using UnityEngine;
using WebRTCStream;

public class Rotate : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Header("Remote control (optional)")]
    [SerializeField] private GenericControlReceiver controlReceiver;
    [Tooltip("The control command's \"target\" string this listens for, e.g. from the dashboard's Send Control form.")]
    [SerializeField] private string controlTarget = "rotate";

    private void Awake()
    {
        if (target == null) target = transform;
    }

    private void OnEnable()
    {
        if (controlReceiver != null)
            controlReceiver.RegisterHandler(controlTarget, HandleRotateCommand);
    }

    private void OnDisable()
    {
        if (controlReceiver != null)
            controlReceiver.UnregisterHandler(controlTarget, HandleRotateCommand);
    }

    /// <summary>Rotate by val degrees around Y. Safe to call directly from code too.</summary>
    public void RotateObject(int val)
    {
        target.Rotate(0, val, 0);
    }

    // GenericControlReceiver hands us a plain string value — parse it back
    // into the int RotateObject expects.
    private void HandleRotateCommand(string value)
    {
        if (int.TryParse(value, out int val))
            RotateObject(val);
        else
            Debug.LogWarning($"[Rotate] Received non-integer rotate value: '{value}'");
    }
}
