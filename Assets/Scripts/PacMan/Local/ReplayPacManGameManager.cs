using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PacMan.Local
{
    public class ReplayPacManGameManager : PacManGameManager
    {
        public string saveFile;
        private IEnumerator<string> file;
        public override void Start()
        {
            file = File.ReadLines(Application.streamingAssetsPath + "/Text/" + saveFile + ".json").GetEnumerator();
        }

        public new void FixedUpdate()
        {
            var next = file.MoveNext();
            var readState = GameStateParser.ReadState(file.Current);
            
            
            
            base.FixedUpdate();
        }

        public new void DropFood(IPacManAgentManager pacManAgentAgent, bool success)
        {
            // Disable DropFood behavior
        }
    }
}