using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Map;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkCommandLine : MonoBehaviour
{
    private NetworkManager netManager;

    void Start()
    {
        netManager = GetComponentInParent<NetworkManager>();

        if (Application.isEditor) return;

        var args = GetCommandlineArgs();

        if (args.TryGetValue("-port", out string port))
        {
            var unityTransport = GetComponent<UnityTransport>();
            unityTransport.SetConnectionData("127.0.0.1", (ushort)Int32.Parse(port));
        }

        if (args.TryGetValue("-mode", out string mode))
        {
            switch (mode.ToLower())
            {
                case "server":
                    netManager.StartServer();
                    break;
                case "host":
                    netManager.StartHost();
                    break;
                case "client":
                    netManager.StartClient();
                    break;
            }
        }


        if (args.TryGetValue("-map", out string map))
        {
            FindObjectsByType<MapManager>(FindObjectsSortMode.None).ToList().ForEach(manager =>
            {
                manager.ClearMap();
                manager.LoadMap(map);
            });
        }
    }

    private Dictionary<string, string> GetCommandlineArgs()
    {
        Dictionary<string, string> argDictionary = new Dictionary<string, string>();

        var args = System.Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; ++i)
        {
            var arg = args[i].ToLower();
            if (arg.StartsWith("-"))
            {
                var value = i < args.Length - 1 ? args[i + 1] : null;
                value = (value?.StartsWith("-") ?? false) ? null : value;

                argDictionary.Add(arg, value);
            }
        }

        return argDictionary;
    }
}