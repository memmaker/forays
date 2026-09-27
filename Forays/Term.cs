/* RVIP web port: replacement for System.Console.
   The game draws into Screen.memory (its own 88x28 cell buffer with Forays colours).
   Term keeps the cursor, and hands the finished cell buffer to a backend when the
   game waits for a key or sleeps. The backend is the browser (web/wasm/WebBackend.cs)
   or a scripted headless feed (web/native/Headless.cs). */
using System;
using System.Collections.Generic;
using OpenTK.Graphics;
namespace Forays{
	public interface ITermBackend{
		bool KeyAvailable();
		ConsoleKeyInfo ReadKey(); //blocks until a key arrives
		void Sleep(int ms); //delay for animations (screen is presented first)
		void Present(int[] cells,int cursor_row,int cursor_col,bool cursor_visible,string info); //info: JSON page state, "" = unchanged
		void Quit(); //the game asked to exit; must not return
		void Sound(string name); //RVIP 6b: a named sound effect
		void Beacon(string query); //RVIP 12: report a finished run (graveyard/leaderboard)
	}
	public static class Term{
		public static ITermBackend Backend;
		public static bool CursorVisible = false;
		public static int CursorTop = 0;
		public static int CursorLeft = 0;
		public static ConsoleColor ForegroundColor = ConsoleColor.Gray;
		public static ConsoleColor BackgroundColor = ConsoleColor.Black;
		public static int BufferWidth{ get{ return Global.SCREEN_W; } set{} }
		public static int BufferHeight{ get{ return Global.SCREEN_H; } set{} }
		public static string Title = "";
		public static bool TreatControlCAsInput = true;
		public static bool AtCommandPrompt = false; //set by the game while it waits for a command (RVIP W4 prompt line)
		public static void SetWindowSize(int w,int h){}
		public static void SetCursorPosition(int left,int top){ CursorLeft = left; CursorTop = top; }
		//Output goes into Screen.memory; nothing is written here.
		public static void Write(string s){}
		public static void Write(char c){}
		public static void Write(string s,params object[] args){}
		static Queue<ConsoleKeyInfo> pushed = new Queue<ConsoleKeyInfo>();
		public static void Push(ConsoleKeyInfo k){ pushed.Enqueue(k); } //read next, before the backend's keys (RVIP menus hand over a command)
		public static void ClearPushed(){ pushed.Clear(); }
		public static bool KeyAvailable{ get{ return pushed.Count > 0 || Backend.KeyAvailable(); } }
		public static ConsoleKeyInfo ReadKey(bool intercept){
			if(pushed.Count > 0) return pushed.Dequeue();
			Present(true);
			return Backend.ReadKey();
		}
		public static void Sleep(int ms){
			Present();
			Backend.Sleep(ms);
		}
		public static void Quit(){
			Present();
			Backend.Quit();
		}
		static int[] cells = new int[Global.SCREEN_H * Global.SCREEN_W * 3];
		public static int RGB(Color c){
			Color4 c4 = Colors.ConvertColor(c);
			int r = (int)Math.Round(c4.R * 255), g = (int)Math.Round(c4.G * 255), b = (int)Math.Round(c4.B * 255);
			return (r << 16) | (g << 8) | b;
		}
		//cells: 3 ints per cell (char, fg 0xRRGGBB, bg 0xRRGGBB), row-major SCREEN_H x SCREEN_W
		public static void Present(){ Present(false); }
		public static void Present(bool with_info){
			int n = 0;
			for(int r=0;r<Global.SCREEN_H;++r){
				for(int c=0;c<Global.SCREEN_W;++c){
					colorchar ch = Screen.Char(r,c);
					cells[n++] = ch.c;
					cells[n++] = RGB(ch.color);
					cells[n++] = RGB(ch.bgcolor);
				}
			}
			bool vis = CursorVisible;
			Actor hero = Actor.player; //RVIP: no cursor on the hero (the map shows where they are)
			if(vis && hero != null && hero.row >= 0 && hero.row < Global.ROWS && hero.col >= 0 && hero.col < Global.COLS
			&& CursorTop == hero.row + Global.MAP_OFFSET_ROWS && CursorLeft == hero.col + Global.MAP_OFFSET_COLS){
				vis = false;
			}
			Backend.Present(cells,CursorTop,CursorLeft,vis,with_info? Rvip.Info() : "");
		}
		//Browser key -> ConsoleKeyInfo. code = KeyboardEvent.code, key = KeyboardEvent.key.
		static Dictionary<char,KeyValuePair<ConsoleKey,bool>> char_keys = null;
		static readonly Dictionary<string,ConsoleKey> code_keys = new Dictionary<string,ConsoleKey>{
			{"Enter",ConsoleKey.Enter},{"NumpadEnter",ConsoleKey.Enter},{"Escape",ConsoleKey.Escape},{"Tab",ConsoleKey.Tab},
			{"Backspace",ConsoleKey.Backspace},{"Space",ConsoleKey.Spacebar},{"ArrowUp",ConsoleKey.UpArrow},{"ArrowDown",ConsoleKey.DownArrow},
			{"ArrowLeft",ConsoleKey.LeftArrow},{"ArrowRight",ConsoleKey.RightArrow},{"Home",ConsoleKey.Home},{"End",ConsoleKey.End},
			{"PageUp",ConsoleKey.PageUp},{"PageDown",ConsoleKey.PageDown},{"Insert",ConsoleKey.Insert},{"Delete",ConsoleKey.Delete},
			{"NumpadAdd",ConsoleKey.Add},{"NumpadSubtract",ConsoleKey.Subtract},{"NumpadMultiply",ConsoleKey.Multiply},
			{"NumpadDivide",ConsoleKey.Divide},{"NumpadDecimal",ConsoleKey.Decimal},{"Minus",ConsoleKey.OemMinus},{"Equal",ConsoleKey.OemPlus},
			{"Comma",ConsoleKey.OemComma},{"Period",ConsoleKey.OemPeriod},{"Slash",ConsoleKey.Oem2},{"Semicolon",ConsoleKey.Oem1},
			{"Quote",ConsoleKey.Oem7},{"BracketLeft",ConsoleKey.Oem4},{"BracketRight",ConsoleKey.Oem6},{"Backslash",ConsoleKey.Oem5},
			{"Backquote",ConsoleKey.Oem3}
		};
		public static bool FromBrowser(string code,string key,bool shift,bool ctrl,bool alt,out ConsoleKeyInfo result){
			result = new ConsoleKeyInfo();
			ConsoleKey k;
			if(code.StartsWith("Numpad") && code.Length == 7 && char.IsDigit(code[6])){ //numpad digits: keep them numpad keys (movement)
				k = ConsoleKey.NumPad0 + (code[6] - '0');
				result = new ConsoleKeyInfo(Input.GetChar(k,shift),k,shift,alt,ctrl);
				return true;
			}
			if(code.StartsWith("F") && code.Length <= 3 && int.TryParse(code.Substring(1),out int f) && f >= 1 && f <= 12){
				k = ConsoleKey.F1 + (f-1);
				result = new ConsoleKeyInfo((char)0,k,shift,alt,ctrl);
				return true;
			}
			if(key != null && key.Length == 1 && !ctrl && !alt){ //printable: find the key+shift that gives this char (any keyboard layout)
				if(char_keys == null){
					char_keys = new Dictionary<char,KeyValuePair<ConsoleKey,bool>>();
					foreach(ConsoleKey ck in Enum.GetValues(typeof(ConsoleKey))){
						if(ck >= ConsoleKey.NumPad0 && ck <= ConsoleKey.NumPad9) continue;
						foreach(bool sh in new bool[]{false,true}){
							char ch = Input.GetChar(ck,sh);
							if(ch != (char)0 && !char_keys.ContainsKey(ch)){
								char_keys[ch] = new KeyValuePair<ConsoleKey,bool>(ck,sh);
							}
						}
					}
				}
				if(char_keys.TryGetValue(key[0],out KeyValuePair<ConsoleKey,bool> kv)){
					result = new ConsoleKeyInfo(key[0],kv.Key,kv.Value,false,false);
					return true;
				}
			}
			if(code.StartsWith("Key") && code.Length == 4){
				k = ConsoleKey.A + (code[3] - 'A');
			}
			else if(code.StartsWith("Digit") && code.Length == 6){
				k = ConsoleKey.D0 + (code[5] - '0');
			}
			else if(!code_keys.TryGetValue(code,out k)){
				return false;
			}
			result = new ConsoleKeyInfo(Input.GetChar(k,shift),k,shift,alt,ctrl);
			return true;
		}
	}
}
