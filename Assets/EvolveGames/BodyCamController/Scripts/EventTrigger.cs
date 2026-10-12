using UnityEngine;
using UnityEngine.Events;
namespace EvolveGames
{
    public class EventTrigger : MonoBehaviour
    {
        public UnityEvent OnExit;
        public UnityEvent OnEnter;

        private void OnTriggerEnter(Collider other)
        {
            if (other.tag == "GameController")
            {
                OnEnter.Invoke();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.tag == "GameController")
            {
                OnExit.Invoke();
            }
        }
    }
}