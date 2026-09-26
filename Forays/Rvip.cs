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
		//Autosave (web, RVIP W5): called while the game waits for a command, with no keys pending.
		//Same state as the 'q' save (the player's turn event is still queued); the game goes on.
		public static void Autosave(){
			if(!Term.AtCommandPrompt || Term.KeyAvailable || Actor.player == null || Global.GAME_OVER) return;
			if(System.IO.File.Exists("forays.sav")) System.IO.File.Delete("forays.sav");
			Global.SaveGame(Actor.B,PhysicalObject.M,PhysicalObject.Q);
			Global.SaveOptions(); //tips already shown, options
		}
		//RVIP 6b: the game names its sounds; the page plays them (off by default).
		public static void Sound(string name){ if(name != null && Term.Backend != null) Term.Backend.Sound(name); }
		public static Actor killer = null; //RVIP 12: damage source of the player's death (Actor.TakeDamage)
		//RVIP 12: one report per finished run (death, win, gave up; not save & quit). Score = depth, as the game's high score list.
		public static void Beacon(int depth,int turn){
			try{
				string ev = Global.BOSS_KILLED? "win" : Global.KILLED_BY == "gave up"? "quit" : "death";
				string q = "g=forays&ev=" + ev + "&name=" + Uri.EscapeDataString(Actor.player_name ?? "");
				if(ev == "death"){
					string k = killer != null && killer != Actor.player? killer.Name.Singular : Global.KILLED_BY;
					if(k.StartsWith("killed by ")) k = k.Substring(10);
					foreach(string a in new[]{"a ","an ","the "}) if(k.StartsWith(a)){ k = k.Substring(a.Length); break; }
					q += "&killer=" + Uri.EscapeDataString(k);
				}
				q += "&depth=" + depth + "&score=" + depth + "&turns=" + turn / 100;
				if(Term.Backend != null) Term.Backend.Beacon(q);
			}
			catch(Exception){}
			killer = null;
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
		/* ---------- page state (RVIP step 5, W0/W4): built by the game, sent with each key wait ---------- */
		public static bool full = true; //a whole-screen view (title, menus, help, character sheet): the page shows the whole screen
		static int log_sent = 0; static string log_last = null;
		static string J(string s){
			var sb = new System.Text.StringBuilder("\"");
			foreach(char c in s){
				if(c == '"' || c == '\\') sb.Append('\\').Append(c);
				else if(c == '\n') sb.Append("\\n");
				else if(c == '\t') sb.Append("\\t");
				else if(c < 32) sb.Append(' ');
				else sb.Append(c);
			}
			return sb.Append('"').ToString();
		}
		static string Hex(Color c){ return J("#" + Term.RGB(c).ToString("x6")); }
		public static string Info(){
			try{ return BuildInfo(); }
			catch(Exception e){ Console.WriteLine("Rvip.Info: " + e); return "{\"full\":" + (full? "true" : "false") + "}"; }
		}
		static string BuildInfo(){
			var sb = new System.Text.StringBuilder("{");
			sb.Append("\"full\":").Append(full? "true" : "false");
			sb.Append(",\"atCmd\":").Append(Term.AtCommandPrompt? "true" : "false");
			//panes of the 88x28 screen, defined here (the game's layout): [row, col, rows, cols]
			sb.Append(",\"panes\":{\"map\":[3,21,25,67],\"side\":[0,0,28,21],\"msg\":[0,21,3,67]}");
			//prompt line: the live message rows (not the dark-grey old ones)
			var pr = new System.Text.StringBuilder();
			for(int r=0;r<3;++r){
				var line = new System.Text.StringBuilder(); bool live = false;
				for(int c=Global.MAP_OFFSET_COLS;c<Global.SCREEN_W;++c){
					colorchar ch = Screen.Char(r,c);
					line.Append(ch.c < ' '? ' ' : ch.c);
					if(ch.c != ' ' && ch.color != Color.DarkGray) live = true;
				}
				if(live) pr.Append(line.ToString().TrimEnd()).Append('\n');
			}
			sb.Append(",\"prompt\":").Append(J(pr.ToString().TrimEnd()));
			Actor p = Actor.player;
			bool in_game = p != null && Actor.B != null && p.row >= 0 && p.row < Global.ROWS && PhysicalObject.M != null && PhysicalObject.M.tile[p.row,p.col] != null;
			if(in_game){
				sb.Append(",\"hero\":[").Append(p.row).Append(',').Append(p.col).Append(']');
				//message log: new lines, or the last line replaced (the game folds repeats as "(xN)")
				List<string> log = Actor.B.GetMessageLog();
				if(log.Count < log_sent){ log_sent = 0; log_last = null; sb.Append(",\"logReset\":true"); }
				var add = new List<string>();
				bool replace = false;
				if(log.Count > 0 && log.Count == log_sent && log[log.Count-1] != log_last && log[log.Count-1].Trim() != ""){ replace = true; add.Add(log[log.Count-1]); }
				for(int i=log_sent;i<log.Count;++i) if(log[i].Trim() != "") add.Add(log[i]);
				if(add.Count > 0){
					sb.Append(",\"log\":[").Append(string.Join(",",add.Select(J))).Append("],\"logReplace\":").Append(replace? "true" : "false");
				}
				log_sent = log.Count; log_last = log.Count > 0? log[log.Count-1] : null;
				//inventory: letter, glyph, name, colour (the item's own)
				sb.Append(",\"inv\":[");
				for(int i=0;i<p.inv.Count;++i){
					Item it = p.inv[i];
					if(i > 0) sb.Append(',');
					sb.Append('[').Append(J(((char)('a'+i)).ToString())).Append(',').Append(J(it.symbol.ToString())).Append(',').Append(J(it.GetName(true,Nym.NameElement.An,Nym.NameElement.Extra))).Append(',').Append(Hex(Colors.ResolveColor(it.color))).Append(']');
				}
				sb.Append(']');
				//equipment
				sb.Append(",\"equip\":[");
				var eq = new List<string>();
				eq.Add("[" + J("Weapon") + "," + J(p.EquippedWeapon.ToString()) + "," + Hex(p.EquippedWeapon.EnchantmentColor()) + "]");
				foreach(Weapon w in p.weapons) if(w != p.EquippedWeapon) eq.Add("[" + J("") + "," + J(w.ToString()) + "," + Hex(Color.Gray) + "]");
				eq.Add("[" + J("Armor") + "," + J(p.EquippedArmor.ToString()) + "," + Hex(p.EquippedArmor.EnchantmentColor()) + "]");
				foreach(Armor a in p.armors) if(a != p.EquippedArmor) eq.Add("[" + J("") + "," + J(a.ToString()) + "," + Hex(Color.Gray) + "]");
				foreach(MagicTrinketType m in p.magic_trinkets) eq.Add("[" + J("Trinket") + "," + J(MagicTrinket.Name(m)) + "," + Hex(Color.Yellow) + "]");
				sb.Append(string.Join(",",eq)).Append(']');
				//visible: monsters and items in view ("M<glyph><name>\t<colour>")
				var vis = new System.Text.StringBuilder();
				foreach(Actor a in PhysicalObject.M.AllActors()){
					if(a == p || !p.CanSee(a)) continue;
					vis.Append('M').Append(a.symbol).Append(a.GetName(false,Nym.NameElement.An)).Append('\t').Append("#" + Term.RGB(Colors.ResolveColor(a.color)).ToString("x6")).Append('\n');
				}
				for(int r=0;r<Global.ROWS;++r) for(int c=0;c<Global.COLS;++c){
					Tile t = PhysicalObject.M.tile[r,c];
					if(t == null || t.inv == null || !p.CanSee(t)) continue;
					vis.Append('I').Append(t.inv.symbol).Append(t.inv.GetName(true,Nym.NameElement.An,Nym.NameElement.Extra)).Append('\t').Append("#" + Term.RGB(Colors.ResolveColor(t.inv.color)).ToString("x6")).Append('\n');
				}
				sb.Append(",\"vis\":").Append(J(vis.ToString()));
			}
			return sb.Append('}').ToString();
		}
	}
}
