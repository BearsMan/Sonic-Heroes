using UnityEngine;
using UnityEngine.Events;

namespace SonicHeroes
{
    public class OrcaRoutePoint : MonoBehaviour
    {
        public enum RoutePointType
        {
            Swim,
            Surface,
            Breach,
            Air,
            Dive,
            Exit
        }

        [Header("Route")]
        [SerializeField] private RoutePointType type = RoutePointType.Swim;
        [SerializeField, Min(0f)] private float moveSpeedOverride;
        [SerializeField, Min(0f)] private float turnSpeedOverride;
        [SerializeField, Min(0f)] private float reachDistanceOverride;
        [SerializeField, Min(0f)] private float pauseDuration;

        [Header("Presentation")]
        [SerializeField] private string animationState;
        [SerializeField] private string animationTrigger;
        [SerializeField] private AudioClip audioOverride;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private GameObject effectOverride;

        [Header("Events")]
        [SerializeField] private UnityEvent onReached;

        public RoutePointType Type => type;
        public float MoveSpeedOverride => moveSpeedOverride;
        public float TurnSpeedOverride => turnSpeedOverride;
        public float ReachDistanceOverride => reachDistanceOverride;
        public float PauseDuration => pauseDuration;
        public string AnimationState => animationState;
        public string AnimationTrigger => animationTrigger;
        public AudioClip AudioOverride => audioOverride;
        public float Volume => volume;
        public GameObject EffectOverride => effectOverride;

        private void OnValidate()
        {
            moveSpeedOverride = Mathf.Max(0f, moveSpeedOverride);
            turnSpeedOverride = Mathf.Max(0f, turnSpeedOverride);
            reachDistanceOverride = Mathf.Max(0f, reachDistanceOverride);
            pauseDuration = Mathf.Max(0f, pauseDuration);
            volume = Mathf.Clamp01(volume);
        }

        internal void InvokeReached()
        {
            onReached?.Invoke();
        }
    }
}
