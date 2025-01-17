using System;
using System.IO;
using Newtonsoft.Json;
using PacMan.Local;
using UnityEngine;

namespace PacMan
{
    public class GameRecorder : MonoBehaviour
    {
        public string filename;
        private string _currentFile;

        public void StartRecording(PacManGameManager pacManGameManager)
        {
            if (enabled)
            {
                _currentFile = Application.streamingAssetsPath + "/Text/" + filename + "_" + System.DateTime.Now.ToLongTimeString().Replace(":", "_").Replace(" ", "") + ".json";
                File.WriteAllText(_currentFile, "");
            }
        }

        public void AppendState(PacManGameManager pacManGameManager)
        {
            if (enabled)
            {
                var writeState = GameStateParser.WriteState(pacManGameManager);
                var json = JsonUtility.ToJson(writeState);
                File.AppendAllText(_currentFile, json + Environment.NewLine);
            }
        }
    }
}