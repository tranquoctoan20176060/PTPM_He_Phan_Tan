/*
Copyright 2021 Heroic Labs

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Nakama;
//using UnityEditor.Experimental.GraphView;
//using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UI;
using static System.Collections.Specialized.BitVector32;
using Client = Nakama.Client;

/// <summary>
/// A singleton class that handles all connectivity with the Nakama server.
/// </summary>
[Serializable]
[CreateAssetMenu]
public class NakamaConnection : ScriptableObject
{
    public string Scheme = "http";
    public string Host = "localhost";
    public int Port = 7350;
    public string ServerKey = "defaultkey";

    private const string SessionPrefName = "nakama.session";
    private const string DeviceIdentifierPrefName = "nakama.deviceUniqueIdentifier";

    public IClient Client;
    public ISession Session;
    public ISocket Socket;

    private string currentMatchmakingTicket;
    private string currentMatchId;

    //public GameObject LogIP;

    /// <summary>
    /// Connects to the Nakama server using device authentication and opens socket for realtime communication.
    /// </summary>
    public async Task Connect()
    {
        // Connect to the Nakama server.
        Client = new Nakama.Client(Scheme, Host, Port, ServerKey, UnityWebRequestAdapter.Instance);
        Debug.Log(Client.Host);
        //LogIP.GetComponent<Text>().text = Host;
        // Attempt to restore an existing user session.
        /*
        var authToken = PlayerPrefs.GetString(SessionPrefName);
        if (!string.IsNullOrEmpty(authToken))
        {
            var session = Nakama.Session.Restore(authToken);
            if (!session.IsExpired)
            {
                Session = session;
                Debug.Log(Session);
            }
        }*/

        // If we weren't able to restore an existing session, authenticate to create a new user session.
        if (Session == null)
        {
            string deviceId;

            // If we've already stored a device identifier in PlayerPrefs then use that.
            if (PlayerPrefs.HasKey(DeviceIdentifierPrefName))
            {
                deviceId = PlayerPrefs.GetString(DeviceIdentifierPrefName);
            }
            else
            {
                // If we've reach this point, get the device's unique identifier or generate a unique one.
                deviceId = SystemInfo.deviceUniqueIdentifier;
                if (deviceId == SystemInfo.unsupportedIdentifier)
                {
                    deviceId = System.Guid.NewGuid().ToString();
                }

                // Store the device identifier to ensure we use the same one each time from now on.
                PlayerPrefs.SetString(DeviceIdentifierPrefName, deviceId);
            }

            // Use Nakama Device authentication to create a new session using the device identifier.
            Session = await Client.AuthenticateDeviceAsync(deviceId);
            //Session = await Client.AuthenticateDeviceAsync(SystemInfo.deviceUniqueIdentifier);

            // Store the auth token that comes back so that we can restore the session later if necessary.
            PlayerPrefs.SetString(SessionPrefName, Session.AuthToken);
        }

        // Open a new Socket for realtime communication.
        Socket = Client.NewSocket();
        await Socket.ConnectAsync(Session, true);

        //Debug.Log(Socket);
        

        /*
        Client = new Nakama.Client(Scheme, Host, Port, ServerKey, UnityWebRequestAdapter.Instance);
        Session = await Client.AuthenticateDeviceAsync(SystemInfo.deviceUniqueIdentifier);
        Socket = Client.NewSocket();
        await Socket.ConnectAsync(Session, true);*/

    }

    /// <summary>
    /// Starts looking for a match with a given number of minimum players.
    /// </summary>
    public async Task FindMatch(int minPlayers = 2)
    {
        // Set some matchmaking properties to ensure we only look for games that are using the Unity client.
        // This is not a required when using the Unity Nakama SDK, 
        // however in this instance we are using it to differentiate different matchmaking requests across multiple platforms using the same Nakama server.
        var matchmakingProperties = new Dictionary<string, string>
        {
            { "engine", "unity" }
        };

        // Add this client to the matchmaking pool and get a ticket.
        var matchmakerTicket = await Socket.AddMatchmakerAsync("+properties.engine:unity", minPlayers, minPlayers, matchmakingProperties);
        currentMatchmakingTicket = matchmakerTicket.Ticket;
    }

    // Perhaps just call this directly from the Socket since it's public already?
    /// <summary>
    /// Cancels the current matchmaking request.
    /// </summary>
    public async Task CancelMatchmaking()
    {
        await Socket.RemoveMatchmakerAsync(currentMatchmakingTicket);
    }

    public async Task ReConnect(String newHost)
    {
        Host = newHost;
        //await Connect();
    }
}
