using System.Collections.Generic;
using UnityEngine;

namespace MP3_Save
{
    [System.Serializable] public class PlayerState
    {
        public Vector3 position;
        public Quaternion rotation;
        public float speed;
        public string action;
        public float verticalVelocity;
        public int animStateHash;
        public float animNormalizedTime;
    }
    
    [System.Serializable] public class CreatureState
    {
        public Vector3 position;
        public Quaternion rotation;
        public float speed;
        public string action;
        public string id;
        public float moveSpeed;
        public string aiState;
        public float stateTimer;
        public int animStateHash;
        public float animNormalizedTime;
        public Vector3 wanderTarget;
    }
    
    [System.Serializable] public class GameSaveData
    {
        public int version;
        public string savedAt;
        public PlayerState player;
        public List<CreatureState> creatures = new List<CreatureState>();
    }
    
}
