using UnityEngine;

namespace Photon.Pun.Demo.Asteroids
{
    public class AsteroidsGame
    {
        public const float ASTEROIDS_MIN_SPAWN_TIME = 5.0f;
        public const float ASTEROIDS_MAX_SPAWN_TIME = 10.0f;

        public const float PLAYER_RESPAWN_TIME = 3.0f;

        public const int PLAYER_MAX_LIVES = 10;

        public const string PLAYER_LIVES = "PlayerLives";
        public const string PLAYER_READY = "IsPlayerReady";
        public const string PLAYER_LOADED_LEVEL = "PlayerLoadedLevel";

        public static Color GetColor(int colorChoice)
        {
            switch (colorChoice)
            {
                case 0: return Color.blue;
                case 1: return Color.red;
                case 2: return Color.yellow;
                case 3: return Color.green;
                case 4: return Color.magenta;
                case 5: return Color.cyan;
                case 6: return Color.white;
                case 7: return Color.grey;
            }

            return Color.black;
        }
    }
}