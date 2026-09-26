/* RVIP web backend: runs inside a module Web Worker (web/worker.js).
   Keys: the page writes them into a SharedArrayBuffer ring; waitKey() blocks with
   Atomics.wait (allowed in a worker), so the game loop stays synchronous.
   Screen: Term.Present() hands the finished cell buffer (char, fg, bg per cell) to JS.
   Files: the game's files live in the runtime's in-memory FS; files named in
   Persistent are mirrored to IndexedDB (the page) whenever they change. */
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
namespace Forays{
	public partial class WebBackend : ITermBackend{
		[JSImport("present","forays")] internal static partial void JsPresent([JSMarshalAs<JSType.MemoryView>] Span<int> cells,int row,int col,bool visible,string info);
		[JSImport("waitKey","forays")] internal static partial string JsWaitKey(int timeout_ms);
		[JSImport("keyAvailable","forays")] internal static partial bool JsKeyAvailable();
		[JSImport("sleep","forays")] internal static partial void JsSleep(int ms);
		[JSImport("storeFile","forays")] internal static partial void JsStoreFile(string name,[JSMarshalAs<JSType.MemoryView>] Span<byte> data);
		[JSImport("deleteFile","forays")] internal static partial void JsDeleteFile(string name);
		[JSImport("initialFiles","forays")] internal static partial string JsInitialFiles(); //names joined by '\n'
		[JSImport("initialFile","forays")] internal static partial byte[] JsInitialFile(string name);
		[JSImport("quit","forays")] internal static partial void JsQuit();
		[JSImport("sound","forays")] internal static partial void JsSound(string name);
		[JSImport("beacon","forays")] internal static partial void JsBeacon(string query);

		public static readonly string[] Persistent = {"forays.sav","options.txt","highscore.txt","keys.txt","name.txt"};
		Dictionary<string,long> stamps = new Dictionary<string,long>();

		public WebBackend(){
			Directory.CreateDirectory("ForaysHelp");
			Assembly asm = Assembly.GetExecutingAssembly();
			foreach(string res in asm.GetManifestResourceNames()){
				string path = null;
				if(res.StartsWith("help/")) path = Path.Combine("ForaysHelp",res.Substring(5));
				else if(res.StartsWith("data/")) path = res.Substring(5);
				if(path == null) continue;
				using(Stream s = asm.GetManifestResourceStream(res)) using(FileStream f = File.Create(path)){ s.CopyTo(f); }
			}
			string names = JsInitialFiles();
			foreach(string name in names.Split('\n')){
				if(name == "seed"){ //test runs: page URL ?seed=N
					int seed;
					if(int.TryParse(System.Text.Encoding.ASCII.GetString(JsInitialFile(name)),out seed)) Utilities.R.SetSeed(seed);
					continue;
				}
				if(name == "" || Array.IndexOf(Persistent,name) < 0) continue;
				File.WriteAllBytes(name,JsInitialFile(name));
			}
			foreach(string name in Persistent) stamps[name] = Stamp(name);
		}
		static long Stamp(string name){
			FileInfo fi = new FileInfo(name);
			if(!fi.Exists) return -1;
			return fi.LastWriteTimeUtc.Ticks ^ (fi.Length << 40);
		}
		public void SyncFiles(){
			foreach(string name in Persistent){
				long st = Stamp(name);
				if(st == stamps[name]) continue;
				stamps[name] = st;
				if(st == -1) JsDeleteFile(name);
				else{
					byte[] data;
					try{ data = File.ReadAllBytes(name); }
					catch(IOException){ stamps[name] = 0; continue; } //still open for writing; retry next time
					JsStoreFile(name,data);
				}
			}
		}
		public bool KeyAvailable(){ return JsKeyAvailable(); }
		public ConsoleKeyInfo ReadKey(){
			SyncFiles();
			while(true){
				string s = JsWaitKey(-1); //"code\tkey\tmods" (mods: s c a)
				string[] p = s.Split('\t');
				if(p.Length < 3) continue;
				if(p[0] == "RvipSave"){ Rvip.Autosave(); SyncFiles(); continue; } //the page asks (every 2 min, tab hidden)
				if(Term.FromBrowser(p[0],p[1],p[2].Contains("s"),p[2].Contains("c"),p[2].Contains("a"),out ConsoleKeyInfo k)){
					return k;
				}
			}
		}
		public void Sleep(int ms){ JsSleep(ms); }
		public void Sound(string name){ JsSound(name); }
		public void Beacon(string query){ JsBeacon(query); }
		public void Present(int[] cells,int row,int col,bool visible,string info){
			JsPresent(cells,row,col,visible,info);
		}
		public void Quit(){
			SyncFiles();
			JsQuit();
			while(true) JsWaitKey(-1); //the page reloads
		}
	}
	public static partial class WebMain{
		public static void Main(string[] args){
			WebBackend b = new WebBackend();
			Term.Backend = b;
			try{
				Game.Main(new string[0]);
			}
			catch(Exception e){
				Console.WriteLine("Forays crashed: " + e);
				throw;
			}
		}
	}
}
