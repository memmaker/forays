/* RVIP ASan substitute: runs the game natively with a scripted random key feed.
   usage: ForaysNative <seed> <keys> [save]   (run in a folder holding ForaysHelp/, options.txt, highscore.txt)
   After <keys> random keys, "save" feeds Escape, q, a, y (save & return to menu), then d (quit).
   Exit code 0 = game quit or keys used up, 2 = unhandled exception, 3 = the game wrote error.txt. */
using System;
using System.IO;
using System.Collections.Generic;
namespace Forays{
	class KeysDone : Exception {}
	class QuitCalled : Exception {}
	class Headless : ITermBackend{
		Random rng; int left; bool save; Queue<ConsoleKeyInfo> script = new Queue<ConsoleKeyInfo>();
		public int[] last, final; public int presents;
		static readonly string pool = "abcdefghijklmnoprstuvwxyzABCDEFGHIJKLMNOPRSTUVWXYZ0123456789<>.,;:?/!@#$%^&*()-=+[]{}\\|'\"`~ \r\x1b\t";
		public Headless(int seed,int keys,bool save_){
			rng = new Random(seed); left = keys; save = save_;
			string sc = Environment.GetEnvironmentVariable("SCRIPT"); //scripted keys first: chars, ~ = Enter, ` = Escape
			if(sc != null) foreach(char c in sc) script.Enqueue(K(c == '~' ? '\r' : c == '`' ? '\x1b' : c));
		}
		static ConsoleKeyInfo K(char ch){
			ConsoleKeyInfo k;
			if(ch == '\r'){ Term.FromBrowser("Enter","Enter",false,false,false,out k); return k; }
			if(ch == '\x1b'){ Term.FromBrowser("Escape","Escape",false,false,false,out k); return k; }
			if(ch == '\t'){ Term.FromBrowser("Tab","Tab",false,false,false,out k); return k; }
			Term.FromBrowser("",ch.ToString(),false,false,false,out k); return k;
		}
		static readonly string[] specials = {"ArrowUp","ArrowDown","ArrowLeft","ArrowRight","Numpad1","Numpad2","Numpad3","Numpad4","Numpad5","Numpad6","Numpad7","Numpad8","Numpad9","NumpadAdd","NumpadSubtract","Home","End","PageUp","PageDown"};
		public bool KeyAvailable(){ return false; }
		public ConsoleKeyInfo ReadKey(){
			if(Environment.GetEnvironmentVariable("GOD") != null && Actor.player != null) Actor.player.attrs[AttrType.INVULNERABLE] = 1; //test only
			if(script.Count > 0){ var kk = script.Dequeue(); if(Environment.GetEnvironmentVariable("TRACE") != null && last != null){ var d = Dump().Split('\n'); Console.WriteLine("KEY " + kk.KeyChar + " " + (Actor.player != null && Actor.player.row >= 0 ? Actor.player.tile().type.ToString() + " " + Rvip.stairs_walk + " " + Actor.player.path.Count : "") + " | " + d[0].Trim() + " | " + d[1].Trim() + " | " + d[2].Trim() + " | " + string.Join(" / ", Array.FindAll(d, l => l.Contains("y/n")))); } return kk; }
			if(left <= 0){
				if(save){
					save = false;
					foreach(char c in "\x1b\x1b\x1bqay") script.Enqueue(K(c));
					for(int i=0;i<20;++i) script.Enqueue(K('\x1b'));
					script.Enqueue(K('d'));
					return script.Dequeue();
				}
				if(final == null && last != null) final = (int[])last.Clone();
				throw new KeysDone();
			}
			--left;
			if(last != null && Dump().Contains("[d] Quit")) return K('a'); //main menu: always (re)start or resume
			int roll = rng.Next(100);
			if(roll < 25){
				ConsoleKeyInfo k; Term.FromBrowser(specials[rng.Next(specials.Length)],"",rng.Next(5)==0,false,false,out k); return k;
			}
			if(roll < 27){
				ConsoleKeyInfo k; Term.FromBrowser("Key" + (char)('A'+rng.Next(26)),"x",false,true,false,out k); return k;
			}
			return K(pool[rng.Next(pool.Length)]);
		}
		public void Sleep(int ms){}
		public void Present(int[] cells,int r,int c,bool vis){ last = cells; ++presents; }
		public void Quit(){ throw new QuitCalled(); }
		public string Dump(){
			int[] last = final ?? this.last;
			if(last == null) return "";
			var sb = new System.Text.StringBuilder();
			for(int r=0;r<Global.SCREEN_H;++r){ for(int c=0;c<Global.SCREEN_W;++c){ int ch = last[(r*Global.SCREEN_W+c)*3]; sb.Append(ch < 32 ? ' ' : (char)ch); } sb.Append('\n'); }
			return sb.ToString();
		}
	}
	static class HeadlessMain{
		static int Main(string[] args){
			int seed = args.Length > 0 ? int.Parse(args[0]) : 1;
			int keys = args.Length > 1 ? int.Parse(args[1]) : 2000;
			bool save = args.Length > 2 && args[2] == "save";
			var h = new Headless(seed,keys,save);
			Term.Backend = h;
			Utilities.R.SetSeed(seed);
			if(File.Exists("error.txt")) File.Delete("error.txt");
			int rc = 0;
			try{ Game.Main(new string[0]); }
			catch(KeysDone){}
			catch(QuitCalled){}
			catch(Exception e){ Console.WriteLine("UNHANDLED: " + e); rc = 2; }
			if(File.Exists("error.txt") && !File.ReadAllText("error.txt").Contains("Forays.KeysDone") && !File.ReadAllText("error.txt").Contains("Forays.QuitCalled")){ Console.WriteLine("error.txt: " + File.ReadAllText("error.txt")); rc = 3; }
			Console.WriteLine("seed " + seed + " presents " + h.presents + " save " + File.Exists("forays.sav") + " rc " + rc);
			if(Environment.GetEnvironmentVariable("DUMP") != null) Console.WriteLine(h.Dump());
			return rc;
		}
	}
}
