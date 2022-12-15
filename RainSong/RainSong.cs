using System;
using BepInEx;
using UnityEngine;
using CustomRegions.Mod;
using RWCustom;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Music;

namespace RainSong
{
    [BepInPlugin("HelloThere.RainSong", "Rain Song", "1.0")]
    public class RainSong : BaseUnityPlugin
    {
        public void OnEnable()
        {
            On.RainWorldGame.ctor += GameCtorPatch;
            On.RainCycle.Update += RainUpdatePatch;
            On.Music.MusicPlayer.RainRequestStopSong += RainStopPatch;
        }

        static bool filesChecked = false;

        static Dictionary<string, string> rainSongDict = new Dictionary<string, string>();

        static void GameCtorPatch(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
        {
            orig.Invoke(self, manager);
            //Check files and populate song dict the first time the player starts the game
            if (!filesChecked)
            {
                foreach (string path in Directory.GetDirectories(Custom.RootFolderDirectory() + "Mods" + Path.DirectorySeparatorChar + "CustomResources"))
                {
                    //Check this region pack's Song folder
                    string songFolder = CRExtras.BuildPath(RemoveDirectoryPath(path), CRExtras.CustomFolder.Songs);
                    if (!Directory.Exists(songFolder)) continue;
                    //Locate music files marked with RAINSONG, extract song name from file name, add it to the dictionary
                    string[] songArray = Directory.GetFiles(songFolder).Where(s => s.Contains("RAINSONG")).ToArray();
                    if (songArray.Length == 0) continue;
                    string song = Path.GetFileNameWithoutExtension(songArray[0]);
                    rainSongDict.Add(song.Remove(2), song);
                    Debug.Log("RainSong:  Registered Rain Song named " + song);
                }
                filesChecked = true;
            }
            //GameCtor is executed on cycle start; if rain song is playing on cycle start, fade it out
            if (manager.musicPlayer != null && manager.musicPlayer.song != null && manager.musicPlayer.song.name.Contains("RAINSONG")) manager.musicPlayer.song.FadeOut(100f);
        }

        static void RainUpdatePatch(On.RainCycle.orig_Update orig, RainCycle self)
        {
            orig.Invoke(self);
            RainWorldGame game = self.world.game;
            MusicPlayer player = game.manager.musicPlayer;
            string songName;
            //Execute rain song only if the rain is approaching, the current room isn't rain-safe, other songs have already faded out, the current room isn't a gate (so this region's rain song won't start while changing region), and the player isn't dead
            if (self.RainApproaching < 0.5f && player != null && game.cameras[0].room.roomSettings.DangerType != RoomRain.DangerType.None && player.song == null && !game.cameras[0].room.abstractRoom.name.Contains("GATE") && !game.Players[0].realizedCreature.dead && rainSongDict.TryGetValue(self.world.region.name, out songName))
            {
                Debug.Log("RainSong:  Playing end of cycle song");
                //Create a new MusicEvent with default values to serve as a dummy to populate the Song object
                MusicEvent dummy = new MusicEvent();
                Song song = new Song(game.manager.musicPlayer, songName, MusicPlayer.MusicContext.StoryMode)
                {
                    fadeOutAtThreat = dummy.maxThreatLevel,
                    Loop = true,
                    priority = 100f,
                    baseVolume = 0.1f,
                    fadeInTime = dummy.fadeInTime,
                    stopAtDeath = true,
                    stopAtGate = true
                };
                player.song = song;
                player.song.playWhenReady = true;
            }
        }

        static void RainStopPatch(On.Music.MusicPlayer.orig_RainRequestStopSong orig, MusicPlayer self)
        {
            //If the current song is a rain song, do not stop it. Otherwise, operate as usual
            if (self.song != null && self.song.name.Contains("RAINSONG")) return;
            orig.Invoke(self);
        }

        static string RemoveDirectoryPath(string path)
        {
            //Remove the last character of the path if it's a directory separator and remove everything before this directory's name
            //TODO: Path.GetDirectoryName already does this?
            if (path[path.Length - 1] == Path.DirectorySeparatorChar)
            {
                path.Remove(path.Length - 1);
            }
            string[] divider = path.Split(Path.DirectorySeparatorChar);
            return divider[divider.Length - 1];
        }
    }
}