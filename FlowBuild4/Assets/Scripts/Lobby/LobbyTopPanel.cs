using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Photon.Pun.Demo.Asteroids
{
    public class LobbyTopPanel : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject connectingDialogue;
        public Text connectionStatusText;
        public Text systemInfoText;
        public Text profile;

        #region UNITY

        private void Awake()
        {
            profile.text = "";
        }

        private void Update()
        {
            //SYSTEM INFO
            if (Time.frameCount % Application.targetFrameRate == 0) //update once per second
            {
                //Region
                string region = "disconnected";

                //Ping
                int ping = 0;
                if (PhotonNetwork.CloudRegion != null && PhotonNetwork.CloudRegion != "")
                {
                    region = PhotonNetwork.CloudRegion;
                    ping = PhotonNetwork.GetPing();
                }

                //Time
                int hour = System.DateTime.Now.Hour;
                string meridiem = "AM";
                if (hour > 12)
                {
                    hour -= 12;
                    meridiem = "PM";
                }

                //Concatenate and print
                systemInfoText.text = "FPS: " + Mathf.RoundToInt(1f / Time.unscaledDeltaTime)
                    + " | Region: " + region
                    + " | Ping: " + ping + "ms"
                    + " | Time: " + hour + ":" + System.DateTime.Now.Minute.ToString("d2") + " " + meridiem;
            }
            
            //CONNECTING INFO
            connectionStatusText.text = "Connecting..."
                + "\n\n" + InsertSpacesInFrontOfCapitals(PhotonNetwork.NetworkClientState.ToString());
                //+ "\nServer type: " + PhotonNetwork.Server;
        }

        public string InsertSpacesInFrontOfCapitals(string text)
        {
            /*
             * Method is by "Binary Worrier"
             * https://stackoverflow.com/questions/272633/add-spaces-before-capital-letters
             */

            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            StringBuilder newText = new StringBuilder(text.Length * 2);
            newText.Append(text[0]);
            for (int i = 1; i < text.Length; i++)
            {
                if (char.IsUpper(text[i]) && text[i - 1] != ' ')
                {
                    newText.Append(' ');
                }
                newText.Append(text[i]);
            }

            return newText.ToString();
        }

        #endregion
    }
}