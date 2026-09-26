/* RVIP additions (web port): the command-prompt hook, stair walking, the
   Enter command menu, the inventory cursor, no [more] stops.
   Called from Actor.InputHuman where the game reads a command. */
using System;
using System.Collections.Generic;
using System.Linq;
namespace Forays{
	public static class Rvip{
		public static bool stairs_walk = false; //'>' away from the stairs: walk there, take them on arrival
		public static bool auto_more = true; //RVIP 3d: [more] prompts don't wait (MessageBuffer.DisplayLines)
		public static bool reopen_inventory = false; //an action chosen from the 'i' list reopens it
		public static ConsoleKeyInfo Key(char c){
			ConsoleKeyInfo k;
			Term.FromBrowser("",c.ToString(),false,false,false,out k);
			return k;
		}
		public static ConsoleKeyInfo Special(string code){
			ConsoleKeyInfo k;
			Term.FromBrowser(code,code,false,false,false,out k);
			return k;
		}
		//The command read in Actor.InputHuman.
		public static ConsoleKeyInfo CommandKey(Actor player){
			if(stairs_walk){
				stairs_walk = false;
				if(player.tile().type == TileType.STAIRS){
					return Key('>');
				}
			}
			if(reopen_inventory){
				reopen_inventory = false;
				if(player.inv.Count > 0 && !MonsterInView(player)){
					return Key('i');
				}
			}
			while(true){
				Term.AtCommandPrompt = true;
				ConsoleKeyInfo command = Input.ReadKey();
				Term.AtCommandPrompt = false;
				if(command.Key == ConsoleKey.Enter && command.Modifiers == 0){
					ConsoleKeyInfo? chosen = CommandMenu();
					PhysicalObject.M.Redraw();
					UI.DisplayStats();
					player.Cursor();
					if(chosen == null) continue;
					return chosen.Value;
				}
				if(command.KeyChar == '<' && command.GetAction().GetCommandChar() == '<'){
					Actor.B.Add("There are no up staircases in Forays: the only way is down. ");
					Actor.B.Print(false);
					player.Cursor();
					continue;
				}
				return command;
			}
		}
		public static bool MonsterInView(Actor player){
			return PhysicalObject.M.AllActors().Any(a => a != player && player.CanSee(a));
		}

		/* ---------- Enter: floating menu of every command (RVIP 3b) ---------- */
		//Grouped like the command list in ForaysHelp/help.txt. Key "Tab" = the Tab key.
		public static readonly string[][] Commands = {
			new string[]{"Look, rest, fight", "Tab","Look around (Tab again: next target)", "r","Rest (heal, repair; once per level)", "t","Torch on/off", "z","Cast a spell", "s","Fire an arrow", "e","Equipment"},
			new string[]{"Items", "i","Inventory", "a","Apply (use) an item", "f","Fling an item", "g","Pick up an item", "d","Drop an item"},
			new string[]{"Moving", "x","Explore automatically", "X","Travel to a location", ">","Take the stairs / walk to them", ".","Wait a turn", "o","Operate terrain", "w","Walk in a direction", "m","Dungeon map"},
			new string[]{"Information", "p","Previous messages", "\\","Known item types", "c","Character info and feats"},
			new string[]{"Game", "q","Quit or save", "=","Options", "?","Help", "-","Command list"},
		};
		struct Line{ public bool header; public string key, text; }
		public static ConsoleKeyInfo? CommandMenu(){
			List<Line> lines = new List<Line>();
			foreach(string[] g in Commands){
				lines.Add(new Line{header = true, text = g[0]});
				for(int i=1;i+1<g.Length;i+=2) lines.Add(new Line{key = g[i], text = g[i+1]});
			}
			int keyw = lines.Where(l => !l.header).Max(l => l.key.Length);
			int w = lines.Max(l => l.header? l.text.Length : keyw + 2 + l.text.Length) + 4; //border + one space each side
			int h = Math.Min(lines.Count + 2,Global.SCREEN_H);
			int view = h - 2;
			int top = (Global.SCREEN_H - h) / 2;
			int left = Global.MAP_OFFSET_COLS + (Global.COLS - w) / 2;
			colorchar[,] saved = Screen.GetCurrentScreen();
			int cur = lines.FindIndex(l => !l.header);
			int scroll = 0;
			ConsoleKeyInfo? result = null;
			bool done = false;
			while(!done){
				if(cur < scroll + 1) scroll = Math.Max(0,cur - 1);
				if(cur >= scroll + view) scroll = cur - view + 1;
				string title = " Commands ";
				Screen.WriteString(top,left,("+" + title + "".PadRight(w - 2 - title.Length,'-') + "+"),Color.Gray);
				for(int r=0;r<view;++r){
					int idx = scroll + r;
					int row = top + 1 + r;
					Screen.WriteChar(row,left,'|',Color.Gray);
					Screen.WriteChar(row,left + w - 1,'|',Color.Gray);
					if(idx >= lines.Count){ Screen.WriteString(row,left + 1,"".PadRight(w - 2)); continue; }
					Line l = lines[idx];
					if(l.header){
						Screen.WriteString(row,left + 1,(" " + l.text).PadRight(w - 2),Color.Yellow);
					}
					else{
						Color bg = idx == cur? Color.DarkBlue : Color.Black;
						Screen.WriteString(row,left + 1," ",Color.Gray,bg);
						Screen.WriteString(row,left + 2,l.key.PadRight(keyw),Color.Cyan,bg);
						Screen.WriteString(row,left + 2 + keyw,("  " + l.text).PadRight(w - 3 - keyw),Color.Gray,bg);
					}
				}
				string foot = scroll + view < lines.Count? "v more" : scroll > 0? "^ more" : "";
				foot = foot == ""? "" : " " + foot + " ";
				Screen.WriteString(top + h - 1,left,("+" + "".PadRight(w - 2 - foot.Length,'-') + foot + "+"),Color.Gray);
				Screen.SetCursorPosition(left + 2,top + 1 + cur - scroll);
				ConsoleKeyInfo k = Input.ReadKey(false);
				switch(k.Key){
				case ConsoleKey.Escape:
				case ConsoleKey.NumPad0:
				case ConsoleKey.Decimal:
					done = true;
					break;
				case ConsoleKey.UpArrow:
				case ConsoleKey.NumPad8:
					do{ cur = (cur - 1 + lines.Count) % lines.Count; } while(lines[cur].header);
					break;
				case ConsoleKey.DownArrow:
				case ConsoleKey.NumPad2:
					do{ cur = (cur + 1) % lines.Count; } while(lines[cur].header);
					break;
				case ConsoleKey.PageUp:
				case ConsoleKey.NumPad9:
					for(int n=0;n<view/2;++n){ do{ cur = (cur - 1 + lines.Count) % lines.Count; } while(lines[cur].header); }
					break;
				case ConsoleKey.PageDown:
				case ConsoleKey.NumPad3:
					for(int n=0;n<view/2;++n){ do{ cur = (cur + 1) % lines.Count; } while(lines[cur].header); }
					break;
				case ConsoleKey.Enter:
				case ConsoleKey.NumPad5:
				case ConsoleKey.NumPad6:
				case ConsoleKey.Spacebar:
					result = lines[cur].key == "Tab"? Special("Tab") : Key(lines[cur].key[0]);
					done = true;
					break;
				case ConsoleKey.Tab:
					result = k;
					done = true;
					break;
				default:
					if(k.KeyChar != (char)0){ //a command's own key runs it
						int i = lines.FindIndex(l => !l.header && l.key.Length == 1 && l.key[0] == k.KeyChar);
						if(i >= 0){
							result = k;
							done = true;
						}
					}
					break;
				}
			}
			Screen.WriteArray(0,0,saved);
			return result;
		}

		/* ---------- inventory cursor (RVIP 3c) ---------- */
		public static int inv_cursor = 0;
		public static bool inv_mode = false; //the 'i' list (letters act) vs an item prompt (letters choose)
		public static void DrawCursor(int count){
			if(count == 0) return;
			if(inv_cursor >= count) inv_cursor = count - 1;
			if(inv_cursor < 0) inv_cursor = 0;
			for(int i=0;i<count;++i){
				int r = i + 1;
				for(int c=0;c<Global.COLS;++c){
					colorchar ch = Screen.MapChar(r,c);
					Color want = i == inv_cursor? Color.DarkBlue : Color.Black;
					if(ch.bgcolor != want){
						ch.bgcolor = want;
						Screen.WriteMapChar(r,c,ch);
					}
				}
			}
		}
	}
}
