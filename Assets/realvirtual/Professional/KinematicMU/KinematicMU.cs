// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz    


using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
#endif

namespace realvirtual
{
    //! Enables physics-based transport of material units on guided surfaces with automatic path following and transitions.
    //! This professional component provides kinematic control for material units (MUs) moving along conveyors, curves,
    //! and other guided transport systems. Automatically detects surface transitions, maintains proper orientation,
    //! and handles complex conveyor networks. Essential for realistic material flow simulation in logistics,
    //! manufacturing, and warehouse automation scenarios with accurate physics-based movement.
    [RequireComponent(typeof(MU))]
    [SelectionBase]
    [DisallowMultipleComponent]
    public class KinematicMU : realvirtualBehavior, ISourceCreated
    {
        [Header("Settings")] 
        public float RaycastLength = 0.3f;
        [SerializeField] public LayerMask RaycastLayer;
        [SerializeField]
        public bool DebugMode;
        [ReadOnly] public bool IsFixed;
        [Header("State")]
        private IGuidedSurface guideSurface; 
        [ShowIf("DebugMode")]public string DebugGuideSurface;
        private MU mu;
        private Transform _transform;
        private IGuidedSurface lastTransport;
        private Rigidbody _rigidbody;
        private float _angleOffset;
        private readonly RaycastHit[] _raycastHits = new RaycastHit[2];
        private bool issource = false;
        [ShowIf("DebugMode")]public GameObject lasthitgo;
        [ShowIf("DebugMode")]public GameObject currenthitgo;
        private float lastdriveposition;
        private bool onguidesurface;
        
        private void OnEnable()
        {
            _transform = GetComponent<Transform>();
            _rigidbody = GetComponentInChildren<Rigidbody>();
            mu = GetComponent<MU>();
            issource = GetComponent<Source>() != null;
            mu.EventMUFix.AddListener(OnMUFixed);
        }

        private void OnMUFixed(MU mu, bool isfixed)
        {
            IsFixed = isfixed;
        }

        private void FixedUpdate()
        {
            if (issource) return;
            Raycast();
            Move();
        }

        private void Reset()
        {
            RaycastLayer = LayerMask.GetMask("rvTransport", "rvSimStatic");
        }

        private void Raycast()
        {
            if (IsFixed) return;
            var raycastPosition = transform.position + Vector3.up * 0.05f;;
            var hits = Physics.RaycastNonAlloc(raycastPosition, Vector3.down,
                _raycastHits, RaycastLength, RaycastLayer);

            if (hits == 0)
            {
                if (onguidesurface)
                {
                    OnLeaveSurface();
                }
                return;
            }

            var hitIndex = 0;
            if (hits > 1)
            {
                hitIndex = GetClosestHitIndex(_raycastHits);
            }
            
            currenthitgo = _raycastHits[hitIndex].transform.gameObject;
            if (currenthitgo != lasthitgo)
            {
                guideSurface = currenthitgo.GetComponentInChildren<IGuidedSurface>();
                if (guideSurface == null)
                {
                   OnLeaveSurface();
                }
                else
                {
                    if (guideSurface.IsSurfaceGuided())
                         OnEnterSurface(guideSurface);
                    else 
                        OnLeaveSurface();
                }
            }
            if (currenthitgo == null && guideSurface != null)
            {
                OnLeaveSurface();
            }
            
            lastTransport = guideSurface;
            lasthitgo = currenthitgo;
        }
        

        private void OnLeaveSurface()
        {
            if (DebugMode) Debug.Log("KinematicMU Leave Surface " + this.name + " " + Time.fixedTime);
             onguidesurface = false;
             guideSurface = null;
             mu.PhysicsOn();
             if (DebugMode) DebugGuideSurface = "";
        }

        private void OnEnterSurface(IGuidedSurface surface)
        {
            if (DebugMode) Debug.Log("KinematicMU Enter Surface " + this.name + " " + Time.fixedTime);
            onguidesurface = true;
            guideSurface = surface;
            if (DebugMode) DebugGuideSurface = guideSurface.ToString();
            lastdriveposition = surface.GetDrive().IsPosition;
            mu.PhysicsOff();
        }
        

        private int GetClosestHitIndex(IReadOnlyList<RaycastHit> hits)
        {
            var distance = Mathf.Infinity;
            var result = 0; 
            for (var i = 0; i < hits.Count; i++)
            {
                if (distance < hits[i].distance) continue;
                distance = hits[i].distance;
                result = i;
            }

            return result;
        }
        
        protected override void OnStopSim()
        {
        }

        protected override void OnStartSim()
        {
        }

        private void Move()
        {
            if (!onguidesurface) return;
            var point =   guideSurface.GetClosestPoint(_transform.position);
            var axis = Quaternion.AngleAxis(_angleOffset, Vector3.up) * Vector3.forward;
            var drivepos = guideSurface.GetDrive().CurrentPosition;
            var normal = guideSurface.GetClosestDirection(_transform.position);
            var newrot = Quaternion.LookRotation(normal, Vector3.up) * Quaternion.AngleAxis(_angleOffset, Vector3.up);
            var deltapos = (drivepos- lastdriveposition)/realvirtualController.Scale ;
            lastdriveposition = drivepos;
            _rigidbody.MoveRotation(newrot);
            _rigidbody.MovePosition(point + normal * deltapos);
        }

    
        private float GetOffsetAngle(IGuidedSurface transport)
        {
            var normal = transport.GetClosestDirection(_transform.position);
            var angle  = Vector3.SignedAngle(normal, transform.forward, Vector3.up);
            return Mathf.Round(angle / 90f) * 90f;
        }

        private void OnDrawGizmos()
        {
            if (!DebugMode) return;
            if (guideSurface == null) return;

            var point = guideSurface.GetClosestPoint(_transform.position);
            var normal = guideSurface.GetClosestDirection(_transform.position);
            var forward =  _rigidbody.transform.right;
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(point, 0.02f);
            Gizmos.DrawLine(transform.position, transform.position + normal*0.2f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * RaycastLength);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + forward * 0.2f);
        }

        public void OnSourceCreated()
        {
            issource = false;
        }
    }
}
