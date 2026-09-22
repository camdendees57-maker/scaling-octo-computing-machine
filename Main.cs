using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow;
using MelonLoader;
using UnityEngine;

namespace WallWalker;

internal sealed class Main : MelonMod
{
    private const float TouchRadius = 0.13f;
    private const float SurfaceSearchDistance = 0.20f;
    private const float SurfaceGravity = 9.81f;
    private const float MoveSpeed = 4.75f;
    private const float MoveAcceleration = 9.0f;
    private const float RotationSpeed = 7.0f;

    private const KeyCode ToggleKey = KeyCode.F;
    private const KeyCode DetachKey = KeyCode.G;
    private const KeyCode JumpOffKey = KeyCode.Space;

    private readonly Collider[] _overlapBuffer = new Collider[32];
    private readonly Dictionary<int, bool> _gravityState = new();

    private RigManager _rigManager;
    private PhysicsRig _physicsRig;

    private Vector3 _surfaceNormal = Vector3.up;
    private bool _attached;
    private float _nextRigSearch;

    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("WallWalker loaded.");
        LoggerInstance.Msg("Touch a solid surface with either hand to attach. F/G detach/attach, Space jumps off.");
    }

    public override void OnUpdate()
    {
        EnsureRig();

        if (_rigManager == null || _physicsRig == null)
            return;

        if (Input.GetKeyDown(DetachKey))
        {
            Detach();
            return;
        }

        if (Input.GetKeyDown(ToggleKey))
        {
            if (_attached)
                Detach();
            else
                TryAttachFromHead();
        }

        if (Input.GetKeyDown(JumpOffKey) && _attached)
        {
            JumpOff();
            return;
        }

        if (_attached)
            TryUpdateSurfaceFromHands();
        else
            TryAttachFromHands();
    }

    public override void OnFixedUpdate()
    {
        if (!_attached || _rigManager == null || _physicsRig == null)
            return;

        ApplySurfaceGravity();
        ApplySurfaceMovement();
        AlignPhysicsToSurface();
    }

    private void EnsureRig()
    {
        if (Time.unscaledTime < _nextRigSearch)
            return;

        _nextRigSearch = Time.unscaledTime + 0.5f;

        var found = GameObject.FindObjectOfType<RigManager>();

        if (found == null)
            return;

        if (_rigManager == found)
            return;

        Detach();

        _rigManager = found;
        _physicsRig = found.physicsRig;

        LoggerInstance.Msg("Found BONELAB player rig.");
    }

    private bool TryAttachFromHands()
    {
        if (_physicsRig == null)
            return false;

        if (TryFindSurface(_physicsRig.leftHand != null ? _physicsRig.leftHand.transform : null, out var leftNormal))
        {
            Attach(leftNormal);
            return true;
        }

        if (TryFindSurface(_physicsRig.rightHand != null ? _physicsRig.rightHand.transform : null, out var rightNormal))
        {
            Attach(rightNormal);
            return true;
        }

        return false;
    }

    private bool TryUpdateSurfaceFromHands()
    {
        if (TryFindSurface(_physicsRig.leftHand != null ? _physicsRig.leftHand.transform : null, out var leftNormal))
        {
            _surfaceNormal = leftNormal.normalized;
            return true;
        }

        if (TryFindSurface(_physicsRig.rightHand != null ? _physicsRig.rightHand.transform : null, out var rightNormal))
        {
            _surfaceNormal = rightNormal.normalized;
            return true;
        }

        return false;
    }

    private bool TryFindSurface(Transform hand, out Vector3 normal)
    {
        normal = Vector3.zero;

        if (hand == null)
            return false;

        int count = Physics.OverlapSphereNonAlloc(
            hand.position,
            TouchRadius,
            _overlapBuffer,
            ~0,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        Collider bestCollider = null;
        Vector3 bestPoint = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            var collider = _overlapBuffer[i];

            if (collider == null || IsPlayerCollider(collider))
                continue;

            var point = collider.ClosestPoint(hand.position);
            var distance = Vector3.Distance(hand.position, point);

            if (distance < bestDistance && distance <= TouchRadius)
            {
                bestDistance = distance;
                bestCollider = collider;
                bestPoint = point;
            }
        }

        if (bestCollider == null)
            return false;

        Vector3 fromSurface = hand.position - bestPoint;

        if (fromSurface.sqrMagnitude > 0.000001f)
        {
            if (Physics.Raycast(
                    hand.position,
                    -fromSurface.normalized,
                    out var hit,
                    SurfaceSearchDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore) &&
                hit.collider == bestCollider)
            {
                normal = hit.normal.normalized;
                return true;
            }

            normal = fromSurface.normalized;
            return true;
        }

        Vector3[] directions =
        {
            hand.forward,
            -hand.forward,
            hand.right,
            -hand.right,
            hand.up,
            -hand.up
        };

        float bestHitDistance = float.MaxValue;
        RaycastHit bestHit = default;
        bool foundHit = false;

        foreach (var direction in directions)
        {
            if (Physics.Raycast(
                    hand.position,
                    direction,
                    out var hit,
                    SurfaceSearchDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore) &&
                hit.collider == bestCollider &&
                hit.distance < bestHitDistance)
            {
                bestHit = hit;
                bestHitDistance = hit.distance;
                foundHit = true;
            }
        }

        if (!foundHit)
            return false;

        normal = bestHit.normal.normalized;
        return true;
    }

    private bool IsPlayerCollider(Collider collider)
    {
        if (_rigManager == null || collider == null)
            return true;

        var t = collider.transform;

        if (t == _rigManager.transform || t.IsChildOf(_rigManager.transform))
            return true;

        if (_physicsRig != null &&
            (t == _physicsRig.transform || t.IsChildOf(_physicsRig.transform)))
            return true;

        return false;
    }

    private void Attach(Vector3 normal)
    {
        if (_physicsRig == null)
            return;

        _attached = true;
        _surfaceNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;

        _gravityState.Clear();

        foreach (var rb in _physicsRig.selfRbs)
        {
            if (rb == null)
                continue;

            _gravityState[rb.GetInstanceID()] = rb.useGravity;
            rb.useGravity = false;
            rb.velocity = Vector3.ProjectOnPlane(rb.velocity, _surfaceNormal);
        }

        LoggerInstance.Msg($"Attached to surface. Normal: {_surfaceNormal}");
    }

    private void Detach()
    {
        if (!_attached && _physicsRig == null)
            return;

        _attached = false;

        if (_physicsRig != null)
        {
            foreach (var rb in _physicsRig.selfRbs)
            {
                if (rb == null)
                    continue;

                if (_gravityState.TryGetValue(rb.GetInstanceID(), out var useGravity))
                    rb.useGravity = useGravity;
            }
        }

        _gravityState.Clear();
        _surfaceNormal = Vector3.up;
    }

    private void ApplySurfaceGravity()
    {
        foreach (var rb in _physicsRig.selfRbs)
        {
            if (rb == null)
                continue;

            if (rb.useGravity)
                rb.useGravity = false;

            rb.AddForce(-_surfaceNormal * SurfaceGravity * rb.mass, ForceMode.Force);
        }
    }

    private void ApplySurfaceMovement()
    {
        var controllerRig = _rigManager.ControllerRig.TryCast<OpenControllerRig>();
        if (controllerRig == null)
            return;

        Vector2 axis = controllerRig.GetPrimaryAxis();
        if (axis.sqrMagnitude < 0.0001f)
            return;

        var head = controllerRig.m_head != null ? controllerRig.m_head : _physicsRig.m_head;
        if (head == null)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(head.forward, _surfaceNormal);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(head.right, _surfaceNormal);

        if (forward.sqrMagnitude < 0.0001f)
            return;

        forward.Normalize();

        Vector3 right = Vector3.Cross(_surfaceNormal, forward).normalized;
        Vector3 moveDirection = right * axis.x + forward * axis.y;

        if (moveDirection.sqrMagnitude < 0.0001f)
            return;

        moveDirection.Normalize();

        var pelvis = _physicsRig.torso.rbPelvis;
        if (pelvis == null)
            return;

        Vector3 currentTangentVelocity = Vector3.ProjectOnPlane(pelvis.velocity, _surfaceNormal);
        Vector3 targetVelocity = moveDirection * MoveSpeed;
        Vector3 velocityError = targetVelocity - currentTangentVelocity;

        float mass = Mathf.Max(1f, _rigManager.avatar != null ? _rigManager.avatar.massTotal : pelvis.mass);
        pelvis.AddForce(velocityError * mass * MoveAcceleration, ForceMode.Force);
    }

    private void AlignPhysicsToSurface()
    {
        var pelvis = _physicsRig.torso.rbPelvis;
        if (pelvis == null)
            return;

        Vector3 currentUp = pelvis.transform.up;
        if (currentUp.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetDelta = Quaternion.FromToRotation(currentUp, _surfaceNormal);
        Quaternion step = Quaternion.Slerp(
            Quaternion.identity,
            targetDelta,
            1f - Mathf.Exp(-RotationSpeed * Time.fixedDeltaTime));

        Vector3 pivot = pelvis.worldCenterOfMass;

        foreach (var rb in _physicsRig.selfRbs)
        {
            if (rb == null)
                continue;

            Vector3 offset = rb.position - pivot;
            rb.MovePosition(pivot + step * offset);
            rb.MoveRotation(step * rb.rotation);
        }
    }

    private void TryAttachFromHead()
    {
        var controllerRig = _rigManager?.ControllerRig.TryCast<OpenControllerRig>();
        var head = controllerRig != null ? controllerRig.m_head : _physicsRig?.m_head;

        if (head == null)
            return;

        if (Physics.Raycast(
                head.position,
                head.forward,
                out var hit,
                5f,
                ~0,
                QueryTriggerInteraction.Ignore) &&
            !IsPlayerCollider(hit.collider))
        {
            Attach(hit.normal);
        }
    }

    private void JumpOff()
    {
        var pelvis = _physicsRig.torso.rbPelvis;
        if (pelvis == null)
            return;

        pelvis.velocity += _surfaceNormal * 7.0f;
        Detach();
    }
}
