using UnityEngine;
using UnityEngine.Events;

// This script detects when another object with a specific tag interacts with this object's trigger collider.
// It invokes custom UnityEvents when the tagged object enters, stays, or exits the trigger area.

public class GoalDetectTrigger : MonoBehaviour
{
    // Tag of the object that should be detected (e.g., "goal")
    [Header("Trigger Collider Tag To Detect")]
    public string tagToDetect = "goal";

    // The value associated with the goal, to be passed in event callbacks
    [Header("Goal Value")]
    public float GoalValue = 1;

    // Reference to this object's Collider component
    private Collider m_col;

    // Custom event type that takes a Collider and a float as arguments
    [System.Serializable]
    public class TriggerEvent : UnityEvent<Collider, float> { }

    // Events that are invoked when another collider enters, stays, or exits this trigger collider
    [Header("Trigger Callbacks")]
    public TriggerEvent onTriggerEnterEvent = new TriggerEvent();
    public TriggerEvent onTriggerStayEvent = new TriggerEvent();
    public TriggerEvent onTriggerExitEvent = new TriggerEvent();

    // Called automatically when another collider enters this object's trigger collider
    private void OnTriggerEnter(Collider col)
    {
        // Check if the other collider has the specified tag
        if (col.CompareTag(tagToDetect))
        {
            // Invoke the custom event with this collider and the goal value
            onTriggerEnterEvent.Invoke(m_col, GoalValue);
        }
    }

    // Called automatically while another collider stays within this object's trigger collider
    private void OnTriggerStay(Collider col)
    {
        if (col.CompareTag(tagToDetect))
        {
            onTriggerStayEvent.Invoke(m_col, GoalValue);
        }
    }

    // Called automatically when another collider exits this object's trigger collider
    private void OnTriggerExit(Collider col)
    {
        if (col.CompareTag(tagToDetect))
        {
            onTriggerExitEvent.Invoke(m_col, GoalValue);
        }
    }

    // Initialize the collider reference when the script instance is loaded
    void Awake()
    {
        m_col = GetComponent<Collider>();
    }
}