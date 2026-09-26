/* RVIP additions (web port): the command-prompt hook, stair walking.
   Called from Actor.InputHuman where the game reads a command. */
using System;
using System.Collections.Generic;
namespace Forays{
	public static class Rvip{
		public static bool stairs_walk = false; //'>' away from the stairs: walk there, take them on arrival
		public static ConsoleKeyInfo Key(char c){
			ConsoleKeyInfo k;
			Term.FromBrowser("",c.ToString(),false,false,false,out k);
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
			while(true){
				Term.AtCommandPrompt = true;
				ConsoleKeyInfo command = Input.ReadKey();
				Term.AtCommandPrompt = false;
				if(command.KeyChar == '<' && command.GetAction().GetCommandChar() == '<'){
					Actor.B.Add("There are no up staircases in Forays: the only way is down. ");
					Actor.B.Print(false);
					player.Cursor();
					continue;
				}
				return command;
			}
		}
	}
}
