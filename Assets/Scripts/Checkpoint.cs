using UnityEngine;

namespace Gamify.Racing
{
    public class Checkpoint : MonoBehaviour
    {
        public int checkpointIndex;
        public DoorController[] doors; // Exactly 4 doors
        public float stopDistanceBeforeDoors = 6.5f;

        public float CheckpointZ => transform.position.z;
        public float WaitingZ => transform.position.z - stopDistanceBeforeDoors;

        public void SetupDoors(QuestionItem question)
        {
            if (doors == null || doors.Length != 4 || question == null || question.options == null)
                return;

            for (int i = 0; i < doors.Length; i++)
            {
                string opt = i < question.options.Length ? question.options[i] : "";
                doors[i].SetupDoor(i, opt);
            }
        }

        public void HighlightDoor(int activeIndex)
        {
            if (doors == null) return;
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null)
                {
                    doors[i].SetHighlighted(i == activeIndex);
                }
            }
        }

        public DoorController GetDoor(int index)
        {
            if (doors != null && index >= 0 && index < doors.Length)
            {
                return doors[index];
            }
            return null;
        }

        public void ResetAllDoors()
        {
            if (doors == null) return;
            foreach (var d in doors)
            {
                if (d != null) d.ResetDoor();
            }
        }
    }
}
