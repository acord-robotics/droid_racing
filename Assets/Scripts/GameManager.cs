using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;


namespace uk.droidbuilders.droid_racing
{
    public class GameManager : MonoBehaviourPunCallbacks
    {
        #region Public Fields
        [Tooltip("The prefab to use for representing the player")]
        public GameObject playerPrefab;
        public static GameManager Instance;
        public Text infoText;
        public Canvas infoBox;
        public GameObject playerList;
        public Canvas resultsBox;
        public Text timeLeftBox;
        public AudioClip start01;
        public AudioClip start02;
        
        [SerializeField]
        public ResultsListingEntry _resultListing;
        public Transform _resultsContent;
        
        
        public int waitBegin = 30; // How long to wait for more players if not minimum number of players
        public int minPlayers = 4; // Minimum number of players in room before automatically starting
        public int raceLength = 3; // Length of a game
        public int startDelay = 5; // Match countdown
        public int endDelay = 10;  // How long to show the results screen before returning to lobby.
        
        #endregion
        
        private Vector3[] spawnPoints = new [] {
          new Vector3(-5f,1f,10f),
          new Vector3(0f,1f,10f),
          new Vector3(-5f,1f,7f),
          new Vector3(0f,1f,7f),
          new Vector3(-5f,1f,4f),
          new Vector3(0f,1f,4f)
        };
        
        public float stateTime;
        private string gameState = "prerace";
        private bool resultsDrawn;
        private GameObject myPlayer;                
        private WebCalls webCalls;
        private AudioSource finishLine;
        
        public const string MAP_PROP_KEY = "map";
        public const string GAME_MODE_PROP_KEY = "gm";   
        public const string ROUND_START_TIME = "StartTime";
        public const string RACE_NAME = "rn";

        #region Photon Callbacks

        public override void OnPlayerEnteredRoom(Player other)
        {
            Debug.LogFormat("GameManager: OnPlayerEnteredRoom() {0}", other.NickName); // not seen if you're the player connecting


            if (PhotonNetwork.IsMasterClient)
            {
                Debug.LogFormat("GameManager: OnPlayerEnteredRoom IsMasterClient {0}", PhotonNetwork.IsMasterClient); // called before OnPlayerLeftRoom


                //LoadArena();
            }
        }


        public override void OnPlayerLeftRoom(Player other)
        {
            Debug.LogFormat("GameManager: OnPlayerLeftRoom() {0}", other.NickName); // seen when other disconnects


            if (PhotonNetwork.IsMasterClient)
            {
                Debug.LogFormat("GameManager: OnPlayerLeftRoom IsMasterClient {0}", PhotonNetwork.IsMasterClient); // called before OnPlayerLeftRoom


                //LoadArena();
            }
        }
        


        /// <summary>
        /// Called when the local player left the room. We need to load the launcher scene.
        /// </summary>
        public override void OnLeftRoom()
        {
            Debug.Log("GameManager: OnLeftRoom");
            SceneManager.LoadScene(0);
            PhotonNetwork.JoinLobby();
        }


        void Start()
        {
            Instance = this;
            webCalls = (WebCalls)GameObject.Find("WebCalls").GetComponent(typeof(WebCalls));
            finishLine = GameObject.FindWithTag("Finish").GetComponent<AudioSource>();
            PhotonNetwork.InstantiateSceneObject("gonk_animated", new Vector3(54f, 2f, 14f ), Quaternion.identity, 0);
            if (playerPrefab == null)
            {
                Debug.LogError("<Color=Red><a>Missing</a></Color> playerPrefab Reference. Please set it up in GameObject 'Game Manager'",this);
            }
            else
            {
                if (PlayerMove.LocalPlayerInstance == null )
                {
                    Debug.LogFormat("GameManager: We are Instantiating LocalPlayer from {0}", SceneManager.GetActiveScene().name);
                    // we're in a room. spawn a character for the local player. it gets synced by using PhotonNetwork.Instantiate
                    int x = PhotonNetwork.PlayerList.Length;
                    myPlayer = (GameObject)PhotonNetwork.Instantiate(this.playerPrefab.name, spawnPoints[x], Quaternion.identity, 0);
                    myPlayer.GetComponent<CameraWork>().enabled = true;
                    myPlayer.GetComponent<AudioListener>().enabled = true;
                }
                else
                {
                    Debug.LogFormat("GameManager: Ignoring scene load for {0}", SceneManagerHelper.ActiveSceneName);
                }
            }
            if (PhotonNetwork.IsMasterClient)
            {
                stateTime = (float)PhotonNetwork.Time;
                Debug.Log("GameManager: startTime set by MasterClient: " + stateTime);
                RoomOptions options = new RoomOptions();
                options.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable {  { "StartTime", stateTime} };
                PhotonNetwork.CurrentRoom.SetCustomProperties(options.CustomRoomProperties);
            }
            else 
            {
                stateTime = float.Parse(PhotonNetwork.CurrentRoom.CustomProperties["StartTime"].ToString());
                Debug.Log("GameManager: startTime got from custom properties is: " + stateTime);
            }
            
            Debug.Log("GameManager: Game Mode is set to: " + PhotonNetwork.CurrentRoom.CustomProperties[GAME_MODE_PROP_KEY]);
            if ((int)PhotonNetwork.CurrentRoom.CustomProperties[GAME_MODE_PROP_KEY] == 1) 
            {
                Debug.Log("GameManager: Freerace selected");
                gameState = "freerace";
                infoBox.gameObject.SetActive(false);
                myPlayer.GetComponent<CharacterController>().enabled = true; 
                resultsBox.gameObject.SetActive(false);
                timeLeftBox.transform.parent.gameObject.SetActive(false);
            }
        }
        
        void Update() 
        {
            switch (gameState)
            {
                case "prerace":
                    PreRace();
                    break;
                case "starting":
                    Starting();
                    break;
                case "racing":
                    Racing();
                    break;
                case "endrace":
                    EndRace();
                    break;
                case "quit":
                    Quit();
                    break;
                case "freerace":
                    FreeRace();
                    break;
                default:
                    Debug.Log("GameManager: Invalid game state");
                    break;
            }
        }
        
        void PreRace() 
        {
            //Debug.Log("GameManager: State - Pre-Race");
            infoBox.gameObject.SetActive(true);
            resultsBox.gameObject.SetActive(false);
            timeLeftBox.transform.parent.gameObject.SetActive(false);
            float currentTime = (float)PhotonNetwork.Time;
            float waitingTime = currentTime - stateTime;
            if ( PhotonNetwork.PlayerList.Length < minPlayers && waitingTime < waitBegin)
            {
                infoText.text = "Waiting for " + (minPlayers - PhotonNetwork.PlayerList.Length).ToString("0") + " more players: " + (waitBegin - waitingTime).ToString("0");
                return;
            }
            else 
            {
                PhotonNetwork.CurrentRoom.IsOpen = false;       // Do this asap, rather than waiting for next update() loop
                PhotonNetwork.CurrentRoom.IsVisible = false;
                stateTime = (float)PhotonNetwork.Time;
                gameState = "starting";
                finishLine.clip = start01;
                InvokeRepeating("CountdownBeep", 1f, 1f);
            }
        }
        
        void Starting()
        {
            //Debug.Log("GameManager: State - Starting");
            float countdown = startDelay - ((float)PhotonNetwork.Time - stateTime);
            if (countdown > 0)
            {
                infoText.text = "Game starting in: " + countdown.ToString("n0");
                //finishLine.Play();
            } 
            else
            {
                CancelInvoke("CountdownBeep");
                finishLine.clip = start02;
                finishLine.Play();
                infoBox.gameObject.SetActive(false);
                timeLeftBox.transform.parent.gameObject.SetActive(true);
                myPlayer.GetComponent<CharacterController>().enabled = true;    // Enable throttle control!
                stateTime = (float)PhotonNetwork.Time;
                gameState = "racing";
            }
      
        }
        
        void Racing()
        {
            //Debug.Log("GameManager: State - Racing");
            CancelInvoke("CountdownBeep");
            float duration = (float)PhotonNetwork.Time - stateTime;
            timeLeftBox.text = (raceLength - duration).ToString("0") + "s";
            if (duration >  raceLength)
            {
                Debug.Log("Race Finished");
                myPlayer.GetComponent<CharacterController>().enabled = false;
                Debug.Log("GameManager: Player laps: " + myPlayer.GetComponent<PlayerMove>().laps);
                StartCoroutine(webCalls.UploadRace(PlayerPrefs.GetString("PlayerEmail"), PhotonNetwork.NickName, myPlayer.GetComponent<PlayerMove>().bestTime, 
                            myPlayer.GetComponent<PlayerMove>().laps, PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.Name, raceLength));
                timeLeftBox.transform.parent.gameObject.SetActive(false);
                resultsBox.gameObject.SetActive(true);
                stateTime = (float)PhotonNetwork.Time;
                gameState = "endrace";
            }
        }
        
        void EndRace()
        {
            //Debug.Log("GameManager: State - EndRace");
            if (!resultsDrawn) 
            {
                DrawResults();
                resultsDrawn = true;
            }
            if ((float)PhotonNetwork.Time > (stateTime + endDelay))
            {
                gameState = "quit";
            }
        }
        
        void FreeRace()
        {
            //Debug.Log("GameManager: State - FreeRace");
        }
        
        void Quit()
        {
            Debug.Log("GameManager: Quit");
            LeaveRoom();
        }

        void DrawResults() 
        {
            var leaderboard = from p in PhotonNetwork.PlayerList
                orderby (int) p.CustomProperties["laps"] ascending
                select p;
            int pos = PhotonNetwork.PlayerList.Length;
            foreach(var player in leaderboard)
            {
                Debug.Log("GameManager: " + player.NickName + " " + player.CustomProperties["laps"] + " Position: " + pos);
                ResultsListingEntry listing = Instantiate(_resultListing, _resultsContent);
                listing.SetResultsInfo(int.Parse(player.CustomProperties["laps"].ToString()), player.NickName, pos);
                pos--;
            }
        }
        #endregion


        #region Public Methods


        public void LeaveRoom()
        {
            Debug.Log("GameManager: LeavingRoom called");
            PhotonNetwork.LeaveRoom();
            //Debug.Log("GameManager: Disconnecting from PhotonNetwork");
            //PhotonNetwork.Disconnect();
        }


        #endregion
        
        #region Private Methods
        void LoadArena()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                Debug.LogError("PhotonNetwork : Trying to Load a level but we are not the master Client");
            }
            Debug.LogFormat("PhotonNetwork : Loading Level : {0}", PhotonNetwork.CurrentRoom.PlayerCount);
            PhotonNetwork.LoadLevel("MainScene");
        } 
        
        void CountdownBeep()
        {
            Debug.Log("Beep!");
            finishLine.Play();
        }
        
        
        #endregion
    }
}