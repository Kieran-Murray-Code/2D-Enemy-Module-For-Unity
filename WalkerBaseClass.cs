using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using DynamicLight2D;
using UnityEngine;
using Random = UnityEngine.Random;
using RotaryHeart.Lib.SerializableDictionary;
using SensorToolkit;
using TLODC_Scripts.Player.Messages;

namespace TLODC_Scripts.Enemies.Base_Classes
{
    
    [Serializable]
    public class StateTransitionsDictonary  : SerializableDictionaryBase<string, WalkerBaseClass.StateData> { }
    [Serializable]
    public class ActiveStateCoroutinesDictonary  : SerializableDictionaryBase<string, Coroutine> { }
    [Serializable]
    public class StateTransitionDictonary : SerializableDictionaryBase<string, WalkerBaseClass.StateTransition>{}

    public class WalkerBaseClass : MonoBehaviourEx, IHandle<OnHitByDisc>, IHandle<OnTopOfMovingPlatform>, IHandle<OnCheckpointLoadStarted>, IHandle<OnCheckpointSave>, IHandle<OnLevelLoad>, IHandle<OnDamageFlashFinished>,IHandle<OnPlayerTransformRecieved>
    {

        public int Health = 1;
        private int _currentHealth;
        public float Gravity ;
        public float JumpStrength;
        public float TerminalVelocity;
        public float Speed;
        public float Acceleration;
        public float Deceleration;
        private float _appliedAcceleration;
        private float _appliedDecceleration;
        public EnemyState State = EnemyState.Idle;
        public enum WalkerStartDirection { Left = -1, Right = 1 }
        public WalkerStartDirection StartDirection = WalkerStartDirection.Right;
        public LayerMask CollisionLayers;
        public bool IgnoreWalls = true;
        public bool ChangeDirectionAtWalls = true;
        public bool IgnoreLedges = true;
        public bool CanAirJump = false;
        public bool UseGravity = true;
        public enum FollowTargets{None,Player,TargetPoint}
        public FollowTargets MovementFollowTargets;
        public Transform MovementTargetPoint;
        public Animator Animator;
        public StateTransitionsDictonary StateTransitionData = new StateTransitionsDictonary();
        public List<string> UnusedStates = new List<string>();
        public List<string> UsedStates = new List<string>();
        public int UnusedStatesIndex;
        public int StartingStateIndex;
        public bool SpawnPickupsOnDeath = false;
        public List<PickUpData> PickUps = new List<PickUpData>();
        public GameObject DeathVisualFx;
        
        private Sensor _sight;
        private Sensor _range;
        public Transform SightSensorTransform;

        public enum SightSensorTrackingTypes{FacingDirection,PlayerDirectionX,PlayerDirectionY,PlayerDirectionXY,Static}

        public SightSensorTrackingTypes SightTracking;
        public SightSensorTrackingTypes GlobalSightTracking;

        public Transform RangeSensorTransform;

        public enum WeakSpotType{Self,External}
        public WeakSpotType WeakSpot;
        private bool _useBaseColliderAsWeakspot = false;
        public List<Transform> WeakSpotTransforms = new List<Transform>();

        private List<int> _animatorBoolHashes = new List<int>();



        public int XDirection = 1;
        public BoxCollider2D BoxCol;
        public Rigidbody2D Rb;
        public Transform Tf;
        public Vector2 MoveDirection, BoxColliderActualSize, IsGroundedBoxSize, IsTouchingWallBoxSize,IsAtEdgeBoxSize, IsAtEdgeBoxPosition, IsTouchingCeilingBoxSize;
        public RaycastHit2D IsGroundedHit, IsTouchingWallHit, IsAtEdgeHit, IsTouchingCeilingHit;
        public bool IsGrounded,IsTouchingCeiling, IsTouchingWall, IsAtEdge,IsJumping, HasBecomeGrounded,HasBecomeAirBorne, HasReachedTarget;
        public float AppliedSpeed, TargetSpeed, AttackRate,RotationSpeed;
        public AudioSource AudioSource;
        public Vector3 SpawnPosition;
        private int  
            RightHash = Animator.StringToHash("Right"), 
            LeftHash = Animator.StringToHash("Left"), 
            AttackingHash = Animator.StringToHash("Attacking"), 
            DetonatingHash = Animator.StringToHash("Detonating"), 
            PatrollingHash = Animator.StringToHash("Patrolling"),
            TakingDamageHash = Animator.StringToHash("TakingDamage"), 
            IdleHash = Animator.StringToHash("Idle"),
            ChargingHash = Animator.StringToHash("Charging"), 
            StunnedHash = Animator.StringToHash("Stunned"),
            SeekingHash = Animator.StringToHash("Seeking"), 
            JumpingHash = Animator.StringToHash("Jumping"), 
            SleepingHash = Animator.StringToHash("Sleeping"),
            FallingHash = Animator.StringToHash("Falling");

        private ActiveStateCoroutinesDictonary _activeCoroutines = new ActiveStateCoroutinesDictonary();
        public bool _damageFlashIsFinished = false;
        private bool _canRecieveHitByDiscMessages = true;
        private bool _playerIsInLineOfSight = false;
        private bool _playerIsInVicinity = false;
        private Transform _player;
        private bool _isRunning = false;
        private bool _isRotating = false;
        private int _activeHash;
        private int _sightDirection;
        private bool canCheckForEdge = false;
        private bool canCheckForWalls = false;

        public enum GravitySurfaces{Ground,Ceiling,RightWall,LeftWall}
        public GravitySurfaces GravitySurface;
        
        public List<float> YVelocityBelowTransitonValues = new List<float>();
        public List<float> YVelocityAboveTransitonValues = new List<float>();
        public List<float> XVelocityBelowTransitonValues = new List<float>();
        public List<float> XVelocityAboveTransitonValues = new List<float>();
        
        public enum DamageTypes{NoDamage,RegularDamage,CriticalDamage}

        public DamageTypes DamageType;





        protected void CalculateBoxCastSizes()
        {
            BoxColliderActualSize = BoxCol.size * Tf.localScale;

            if (GravitySurface == GravitySurfaces.Ground || GravitySurface == GravitySurfaces.Ceiling)
            {
                IsGroundedBoxSize = new Vector2(BoxColliderActualSize.x*0.75f, BoxColliderActualSize.y *0.5f);
                
                IsTouchingCeilingBoxSize = new Vector2(BoxColliderActualSize.x, BoxColliderActualSize.y*0.5f);
                IsTouchingWallBoxSize = new Vector2(BoxColliderActualSize.x *0.5f, BoxColliderActualSize.y);
                IsTouchingWallBoxSize.y = IsTouchingWallBoxSize.y *0.95f; // Only using a % of the box's height to stop collisions being detected from wall below.
                IsAtEdgeBoxSize = new Vector2(BoxColliderActualSize.x *0.25f, BoxColliderActualSize.y*0.5f);
            }
            else if (GravitySurface == GravitySurfaces.LeftWall || GravitySurface == GravitySurfaces.RightWall)
            {
                IsGroundedBoxSize = new Vector2(BoxColliderActualSize.y*0.5f,BoxColliderActualSize.x*0.75f);
                
                IsTouchingCeilingBoxSize = new Vector2(BoxColliderActualSize.x*0.5f, BoxColliderActualSize.y);
                IsTouchingWallBoxSize = new Vector2(BoxColliderActualSize.x, BoxColliderActualSize.y*0.5f);
                IsTouchingWallBoxSize.y = IsTouchingWallBoxSize.x*0.95f; // Only using a % of the box's height to stop collisions being detected from wall below.
                IsAtEdgeBoxSize = new Vector2(BoxColliderActualSize.x *0.5f, BoxColliderActualSize.y*0.25f);
            }
        }
        public void OnEnable()
        {
            Messenger.Subscribe(this);
            if (_sight != null)
            {
                _sight.OnDetected.AddListener(OnEnterLineOfSight);
                _sight.OnLostDetection.AddListener(OnExitsLineOfSight);
            }

            if (_range != null)
            {
                _range.OnDetected.AddListener(OnEntersVicinity);
                _range.OnLostDetection.AddListener(OnExitsVicinity);
            }
            

        }
        public void OnDisable()
        {
            Messenger.Unsubscribe(this);
            if (_sight != null)
            {
                _sight.OnDetected.RemoveListener(OnEnterLineOfSight);
                _sight.OnLostDetection.RemoveListener(OnExitsLineOfSight);
            }

            if (_range != null)
            {
                _range.OnDetected.RemoveListener(OnEntersVicinity);
                _range.OnLostDetection.RemoveListener(OnExitsVicinity);
            }

        }
        public override void Awake()
        {
            base.Awake();
            BoxCol = GetComponent<BoxCollider2D>();
            Rb = GetComponent<Rigidbody2D>();
            Tf = GetComponent<Transform>();

            if (SightSensorTransform != null)
            {
                if (SightSensorTransform.GetComponent<TriggerSensor2D>() != null)
                {
                    _sight = SightSensorTransform.GetComponent<TriggerSensor2D>();
                }
            }

            if (RangeSensorTransform != null)
            {
                if (RangeSensorTransform.GetComponent<RangeSensor2D>() != null)
                {
                    _range = RangeSensorTransform.GetComponent<RangeSensor2D>();
                }
            }
            
            CalculateBoxCastSizes();
            XDirection = (int)StartDirection;
            AudioSource = GetComponent<AudioSource>();
            
            _animatorBoolHashes.Clear();

            _animatorBoolHashes.Add(RightHash);
            _animatorBoolHashes.Add(LeftHash);
            _animatorBoolHashes.Add(AttackingHash); //1
            _animatorBoolHashes.Add(PatrollingHash);//2
            _animatorBoolHashes.Add(TakingDamageHash);//3
            _animatorBoolHashes.Add(DetonatingHash);//4
            _animatorBoolHashes.Add(IdleHash);//5
            _animatorBoolHashes.Add(ChargingHash);//6
            _animatorBoolHashes.Add(SeekingHash);//7
            _animatorBoolHashes.Add(StunnedHash);//8
            _animatorBoolHashes.Add(JumpingHash);//9
            _animatorBoolHashes.Add(SleepingHash);//10
            _animatorBoolHashes.Add(FallingHash);

            for (var i = 0; i < Enum.GetValues(typeof(EnemyState)).Length; i++)
            {
                _activeCoroutines.Add(((EnemyState)i).ToString(), null);
            }
            

            
            

            _currentHealth = Health;
        

        }
        public void Start()
        {
            StartCoroutine(DelayedStart());
        }
        IEnumerator DelayedStart()
        {
            yield return  new WaitForSeconds(0.5f);
            SpawnPosition = Tf.position;
            ChangeState(UsedStates[StartingStateIndex]);
            StartCoroutine(UpdateAnimator());
            DropTargetPositionObjects();
            _isRunning = true;
        }

        public void DropTargetPositionObjects()
        {
            foreach (var std in StateTransitionData)
            {
                if (std.Value.MovementTargetPoint != null)
                {
                    std.Value.MovementTargetPoint.parent = null;
                }
            }
        }
        
        public void FixedUpdate()
        {
            if (_isRunning)
            {

                
                
                if (_playerIsInLineOfSight)
                {
                    if (StateTransitionData[State.ToString()].StateTransitions
                        .ContainsKey(EnemyStateTrigger.PlayerEntersLineOfSight.ToString()))
                    {
                        ChangeState(StateTransitionData[State.ToString()]
                            .StateTransitions[EnemyStateTrigger.PlayerEntersLineOfSight.ToString()].NewState);
                    }
                }
                else
                {
                    if (StateTransitionData[State.ToString()].StateTransitions
                        .ContainsKey(EnemyStateTrigger.PlayerExitsLineOfSight.ToString()))
                    {
                        ChangeState(StateTransitionData[State.ToString()]
                            .StateTransitions[EnemyStateTrigger.PlayerExitsLineOfSight.ToString()].NewState);
                    }
                }

                if (_playerIsInVicinity)
                {
                    if (StateTransitionData[State.ToString()].StateTransitions
                        .ContainsKey(EnemyStateTrigger.PlayerEntersVicinity.ToString()))
                    {
                        ChangeState(StateTransitionData[State.ToString()]
                            .StateTransitions[EnemyStateTrigger.PlayerEntersVicinity.ToString()].NewState);
                    }
                }
                else
                {
                    if (StateTransitionData[State.ToString()].StateTransitions
                        .ContainsKey(EnemyStateTrigger.PlayerExitsVicinity.ToString()))
                    {
                        ChangeState(StateTransitionData[State.ToString()]
                            .StateTransitions[EnemyStateTrigger.PlayerExitsVicinity.ToString()].NewState);
                    }
                }

                if (SightSensorTransform != null)
                {

                    
                    if (SightTracking == SightSensorTrackingTypes.FacingDirection)
                    {
                        if (XDirection == 1)
                        {
                            _sightDirection = 1;
                        }
                        else if (XDirection == -1)
                        {
                            _sightDirection = -1;
                        }
                        
                        SightSensorTransform.localRotation = Quaternion.Euler(0, 0, 90 * -_sightDirection);

                    }
                    else if (SightTracking == SightSensorTrackingTypes.PlayerDirectionX)
                    {
                        if (Tf.position.x < _player.position.x)
                        {
                            _sightDirection = 1;
                        }
                        else if (Tf.position.x > _player.position.x)
                        {
                            _sightDirection = -1;
                        }
                        
                        SightSensorTransform.localRotation = Quaternion.Euler(0, 0, 90 * -_sightDirection);

                    }
                    else if (SightTracking == SightSensorTrackingTypes.PlayerDirectionY)
                    {
                        if (Tf.position.y < _player.position.y)
                        {
                            _sightDirection = 0;
                        }
                        else if (Tf.position.y > _player.position.y)
                        {
                            _sightDirection = 180;
                        }
                        
                        SightSensorTransform.localRotation = Quaternion.Euler(0, 0, _sightDirection); 
                    }
                    else if (SightTracking == SightSensorTrackingTypes.PlayerDirectionXY)
                    {
                        var directionToPlayer = (Tf.position - _player.position).normalized;
                        var angleToPlayer = Mathf.Atan2(directionToPlayer.y,directionToPlayer.x);
                        angleToPlayer *= Mathf.Rad2Deg;
                        angleToPlayer += 90;
                        SightSensorTransform.localRotation = Quaternion.Euler(new Vector3(0,0,angleToPlayer));
                    }
                    else if (SightTracking == SightSensorTrackingTypes.Static)
                    {
                        
                    }
                    

                }

                if (AppliedSpeed < TargetSpeed)
                {
                    if (AppliedSpeed + _appliedAcceleration * Time.fixedDeltaTime < TargetSpeed)
                    {
                        AppliedSpeed += _appliedAcceleration * Time.fixedDeltaTime;
                    }
                    else
                    {
                        AppliedSpeed = TargetSpeed;
                    }
                }
                else if (AppliedSpeed > TargetSpeed)
                {
                    if (AppliedSpeed - _appliedDecceleration * Time.fixedDeltaTime > TargetSpeed)
                    {
                        AppliedSpeed -= _appliedDecceleration * Time.fixedDeltaTime;
                    }
                    else
                    {
                        AppliedSpeed = TargetSpeed;
                    }
                }
                else
                {
                    AppliedSpeed = TargetSpeed;
                }

                if (GravitySurface == GravitySurfaces.Ceiling || GravitySurface == GravitySurfaces.Ground)
                {
                    IsGroundedHit = Physics2D.BoxCast(Rb.position, IsGroundedBoxSize, 0, -Tf.up,
                        IsGroundedBoxSize.y / 2 + Mathf.Abs(Mathf.Abs(MoveDirection.y) * Time.fixedDeltaTime),
                        CollisionLayers);
                    
                    IsTouchingWallHit = Physics2D.BoxCast(Rb.position, IsTouchingWallBoxSize, 0, Tf.right * XDirection,
                        IsTouchingWallBoxSize.x / 2 + Mathf.Abs(MoveDirection.x * Time.fixedDeltaTime), CollisionLayers);
                    
                    IsTouchingCeilingHit = Physics2D.BoxCast(Rb.position, IsTouchingCeilingBoxSize, 0, Tf.up,
                        IsTouchingCeilingBoxSize.y / 2 + Mathf.Abs(Mathf.Abs(MoveDirection.y) * Time.fixedDeltaTime),
                        CollisionLayers);
                }
                if (GravitySurface == GravitySurfaces.LeftWall || GravitySurface == GravitySurfaces.RightWall)
                {
                    IsGroundedHit = Physics2D.BoxCast(Rb.position, IsGroundedBoxSize, 0, -Tf.up,
                        IsGroundedBoxSize.x / 2 + Mathf.Abs(Mathf.Abs(MoveDirection.y) * Time.fixedDeltaTime),
                        CollisionLayers); 
                    
                    IsTouchingWallHit = Physics2D.BoxCast(Rb.position, IsTouchingWallBoxSize, 0, Tf.right * XDirection,
                        IsTouchingWallBoxSize.y / 2 + Mathf.Abs(MoveDirection.x * Time.fixedDeltaTime), CollisionLayers);
                    
                    IsTouchingCeilingHit = Physics2D.BoxCast(Rb.position, IsTouchingCeilingBoxSize, 0, Tf.up,
                        IsTouchingCeilingBoxSize.x / 2 + Mathf.Abs(Mathf.Abs(MoveDirection.y) * Time.fixedDeltaTime),
                        CollisionLayers);
                }
                IsGrounded = IsGroundedHit;



                if (MoveDirection.y > 0.0f)
                {
                    IsTouchingCeiling = IsTouchingCeilingHit;
                }
                else
                {
                    IsTouchingCeiling = false;
                }


                
                if (canCheckForWalls)
                {
                    IsTouchingWall = IsTouchingWallHit;
                }

                if (!IsTouchingWallHit)
                {
                    canCheckForWalls = true;
                }

                else
                {
                    canCheckForWalls = false;
                }
                
                IsTouchingWall = IsTouchingWallHit;



                if (IsTouchingCeiling)
                {
                    MoveDirection.y = 0.0f;
                }


                foreach (var t in YVelocityBelowTransitonValues)
                {
                    if (MoveDirection.y < t)
                    {
                        if (StateTransitionData[State.ToString()].StateTransitions
                            .ContainsKey(EnemyStateTrigger.YVelocityIsBelow.ToString() + t))
                        {
                            ChangeState(StateTransitionData[State.ToString()]
                                .StateTransitions[EnemyStateTrigger.YVelocityIsBelow.ToString() + t]
                                .NewState);
                            break;
                        }
                    }
                }
                foreach (var t in YVelocityAboveTransitonValues)
                {
                    if (MoveDirection.y > t)
                    {
                        if (StateTransitionData[State.ToString()].StateTransitions
                            .ContainsKey(EnemyStateTrigger.YVelocityIsAbove.ToString() + t))
                        {
                            ChangeState(StateTransitionData[State.ToString()]
                                .StateTransitions[EnemyStateTrigger.YVelocityIsAbove.ToString() + t]
                                .NewState);
                            break;
                        }
                    }
                }
                foreach (var t in XVelocityAboveTransitonValues)
                {
                    if (MoveDirection.x > t)
                    {
                        if (StateTransitionData[State.ToString()].StateTransitions
                            .ContainsKey(EnemyStateTrigger.XVelocityIsAbove.ToString() + t))
                        {
                            ChangeState(StateTransitionData[State.ToString()]
                                .StateTransitions[EnemyStateTrigger.XVelocityIsAbove.ToString() + t]
                                .NewState);
                            break;
                        }
                    }
                }
                foreach (var t in XVelocityBelowTransitonValues)
                {
                    if (MoveDirection.x < t)
                    {
                        if (StateTransitionData[State.ToString()].StateTransitions
                            .ContainsKey(EnemyStateTrigger.XVelocityIsBelow.ToString() + t))
                        {
                            ChangeState(StateTransitionData[State.ToString()]
                                .StateTransitions[EnemyStateTrigger.XVelocityIsBelow.ToString() + t]
                                .NewState);
                            break;
                        }
                    }
                }

                if (IsGrounded)
                {

                    if (GravitySurface == GravitySurfaces.Ground || GravitySurface == GravitySurfaces.Ceiling)
                    {
                        IsAtEdgeBoxPosition =
                            new Vector2(Rb.position.x + (BoxColliderActualSize.x / 2 + IsAtEdgeBoxSize.x / 2) * XDirection* Tf.up.y,
                                Rb.position.y);
                        IsAtEdgeHit = Physics2D.BoxCast(IsAtEdgeBoxPosition, IsAtEdgeBoxSize, 0, -Tf.up,
                            BoxColliderActualSize.y / 2, CollisionLayers);                        
                    }
                    if (GravitySurface == GravitySurfaces.LeftWall || GravitySurface == GravitySurfaces.RightWall)
                    {
                        IsAtEdgeBoxPosition =
                            new Vector2(Rb.position.x, Rb.position.y + (BoxColliderActualSize.y*0.5f + IsAtEdgeBoxSize.y*0.5f) * XDirection*Tf.right.y);
                        IsAtEdgeHit = Physics2D.BoxCast(IsAtEdgeBoxPosition, IsAtEdgeBoxSize, 0, -Tf.up,
                            BoxColliderActualSize.x*0.5f, CollisionLayers);                        
                    }
                    
                    
                    if (IsAtEdge)
                    {
                        if (IgnoreLedges == false)
                        {
                            if (MovementFollowTargets != FollowTargets.Player)
                            {
                                XDirection *= -1;
                            }
                            else
                            {
                                XDirection = 0;
                            }
                        }

                        if (StateTransitionData[State.ToString()].StateTransitions
                            .ContainsKey(EnemyStateTrigger.ReachedLedge.ToString()))
                        {
                            ChangeState(StateTransitionData[State.ToString()]
                                .StateTransitions[EnemyStateTrigger.ReachedLedge.ToString()].NewState);
                        }

                        IsAtEdge = false;
                    }
                    
                    if (canCheckForEdge)
                    {
                        IsAtEdge = !IsAtEdgeHit;
                    }

                    if (IsAtEdgeHit)
                    {
                        canCheckForEdge = true;
                    }

                    else
                    {
                        canCheckForEdge = false;
                    }
                    
                    
                    
                    
                    if (HasBecomeGrounded == false && HasBecomeAirBorne)
                    {
                        HasBecomeGrounded = true;
                        HasBecomeAirBorne = false;
                        IsJumping = false;
                        if (StateTransitionData[State.ToString()].StateTransitions
                            .ContainsKey(EnemyStateTrigger.BecameGrounded.ToString()))
                        {
                            ChangeState(StateTransitionData[State.ToString()]
                                .StateTransitions[EnemyStateTrigger.BecameGrounded.ToString()].NewState);
                        }
                        

                    }

                    if (StateTransitionData[State.ToString()].useGlobalSpeedSettings)
                    {
                        TargetSpeed = Speed;
                        _appliedAcceleration = Acceleration;
                        _appliedDecceleration = Deceleration;
                    }
                    else
                    {
                        TargetSpeed = StateTransitionData[State.ToString()].Speed;
                        _appliedAcceleration = StateTransitionData[State.ToString()].Acceleration;
                        _appliedDecceleration = StateTransitionData[State.ToString()].Decceleration;
                    }

                    if (IsJumping == false)
                    {
                        MoveDirection.y = 0;
                        if (StateTransitionData[State.ToString()].StateTransitions
                            .ContainsKey(EnemyStateTrigger.CollisionWithCeiling.ToString()))
                        {
                            ChangeState(StateTransitionData[State.ToString()]
                                .StateTransitions[EnemyStateTrigger.CollisionWithCeiling.ToString()].NewState);
                        }
                    }

                    MoveDirection.x = AppliedSpeed * XDirection;

                    if (MovementFollowTargets != FollowTargets.None)
                    {
                        if (MovementTargetPoint != null)
                        {
                            if (MovementTargetPoint.position.x < Tf.position.x)
                            {
                                XDirection = -1;
                            }
                            else if (MovementTargetPoint.position.x > Tf.position.x)
                            {
                                XDirection = 1;
                            }
                            else
                            {
                                XDirection = 0;
                            }
                        }
                    }

                }
                else
                {
                    
                    HasBecomeGrounded = false;
                    if (HasBecomeAirBorne == false)
                    {
                        HasBecomeAirBorne = true;
                        if (StateTransitionData[State.ToString()].StateTransitions
                            .ContainsKey(EnemyStateTrigger.BecameAirBorne.ToString()))
                        {
                            ChangeState(StateTransitionData[State.ToString()]
                                .StateTransitions[EnemyStateTrigger.BecameAirBorne.ToString()].NewState);
                        }
                    }

                    //TargetSpeed = 0;

                    if (Mathf.Abs(MoveDirection.y - Gravity * Time.fixedDeltaTime) < TerminalVelocity)
                    {
                        MoveDirection.y -= Gravity * Time.fixedDeltaTime;
                    }
                    else
                    {
                        MoveDirection.y = -TerminalVelocity;
                    }

                    if (IsTouchingWall)
                    {
                        if (MoveDirection.y < 0)
                        {
                            MoveDirection.x = 0;
                            AppliedSpeed = 0;
                            TargetSpeed = 0;
                        }
                    }
                    else
                    {
                        MoveDirection.x = AppliedSpeed * XDirection;
                    }
                    
                    // Air Velocity
                    //Rb.MovePosition(Rb.position + MoveDirection * Time.fixedDeltaTime);
                }
                
                if (IsTouchingWall && !IgnoreWalls)
                {
                    var wallAllignPosition = IsTouchingWallHit.point;

                    if (GravitySurface == GravitySurfaces.Ground || GravitySurface == GravitySurfaces.Ceiling)
                    {
                        wallAllignPosition.x -= BoxColliderActualSize.x / 2 * XDirection*Tf.up.y;
                        wallAllignPosition.y = Rb.position.y;
                    }
                    else if (GravitySurface == GravitySurfaces.LeftWall || GravitySurface == GravitySurfaces.RightWall)
                    {
                        wallAllignPosition.y -= BoxColliderActualSize.y*0.5f * XDirection*Tf.right.y;
                        wallAllignPosition.x = Rb.position.x; 
                    }
                    Rb.position = wallAllignPosition;
                    
                    if (ChangeDirectionAtWalls)
                    {
                        if (MovementFollowTargets != FollowTargets.Player)
                        {
                            XDirection *= -1;
                        }
                        else
                        {
                            XDirection = 0;
                        }
                    }

                    if (StateTransitionData[State.ToString()].StateTransitions
                        .ContainsKey(EnemyStateTrigger.CollisionWithWall.ToString()))
                    {
                        ChangeState(StateTransitionData[State.ToString()]
                            .StateTransitions[EnemyStateTrigger.CollisionWithWall.ToString()].NewState);
                    }

                    IsTouchingWall = false;
                }
                
                
                if (MovementFollowTargets == FollowTargets.None)
                {
                    Vector2 surfaceRelativeMoveDirection = MoveDirection;

                    if (GravitySurface == GravitySurfaces.Ground)
                    {
                        surfaceRelativeMoveDirection = new Vector2(MoveDirection.x,MoveDirection.y);
                    }
                    else if (GravitySurface == GravitySurfaces.Ceiling)
                    {
                        surfaceRelativeMoveDirection = new Vector2(-MoveDirection.x,-MoveDirection.y);
                    }
                    else if (GravitySurface == GravitySurfaces.LeftWall)
                    {
                        surfaceRelativeMoveDirection = new Vector2(MoveDirection.y,-MoveDirection.x);
                    }
                    else if (GravitySurface == GravitySurfaces.RightWall)
                    {
                        surfaceRelativeMoveDirection = new Vector2(-MoveDirection.y,MoveDirection.x);
                    }
                    
                    Rb.MovePosition(Rb.position + surfaceRelativeMoveDirection * Time.fixedDeltaTime);
                }
                else
                {
                    if (MovementTargetPoint != null)
                    {
                        if (Mathf.Abs(Rb.position.x - MovementTargetPoint.position.x) >= 0.5f)
                        {
                            HasReachedTarget = false;
                        }

                        if (Mathf.Abs(Rb.position.x - MovementTargetPoint.position.x) < Mathf.Abs(MoveDirection.x*Time.deltaTime) && HasReachedTarget == false)
                        {
                            Rb.position = new Vector3(MovementTargetPoint.position.x,Tf.position.y,Tf.position.z);
                            if (StateTransitionData[State.ToString()].StateTransitions
                                .ContainsKey(EnemyStateTrigger.ReachedTarget.ToString()))
                            {
                                ChangeState(StateTransitionData[State.ToString()]
                                    .StateTransitions[EnemyStateTrigger.ReachedTarget.ToString()].NewState);
                            }

                            HasReachedTarget = true;
                        }
                        else
                        {
                            if (HasReachedTarget == false)
                            {
                                Rb.MovePosition(Rb.position + MoveDirection * Time.fixedDeltaTime);
                            }
                        }
                    }
                }
                
            }
        }
       

        public void StopAllStateCoroutinesExceptCurrent()
        {
            foreach (var activeCoroutine in _activeCoroutines) 
            {
                if (activeCoroutine.Key == State.ToString())
                {
                }
                else
                {
                    if (_activeCoroutines[activeCoroutine.Key] != null)
                    {
                        StopCoroutine(_activeCoroutines[activeCoroutine.Key]);
                    }
                }
            }


            
        }       
        public void Handle(OnHitByDisc message)
        {
            if (_useBaseColliderAsWeakspot)
            {
                OnHitByDisc();
            }
        }

        public void OnHitByDisc()
        {
            if (!_canRecieveHitByDiscMessages) return;

            if (StateTransitionData[State.ToString()].StateTransitions
                .ContainsKey(EnemyStateTrigger.IsHitByDisc.ToString()))
            {
                if (StateTransitionData[State.ToString()].StateTransitions[EnemyStateTrigger.IsHitByDisc.ToString()].NewState == "TakingDamage")
                {
                    
                }
                else
                {
                    DamageType = DamageTypes.NoDamage;
                    SendMessage("StartDamageFlash",this, SendMessageOptions.DontRequireReceiver);
                }
                
                ChangeState(StateTransitionData[State.ToString()].StateTransitions[EnemyStateTrigger.IsHitByDisc.ToString()].NewState);
            }
            else
            {
                DamageType = DamageTypes.NoDamage;
                SendMessage("StartDamageFlash",this, SendMessageOptions.DontRequireReceiver);
            }
            _canRecieveHitByDiscMessages = false;
            StartCoroutine(ResetDiscHitDetection()); 
        }
        
        private void SetAnimatorBool(IEnumerable<int> hashes)
        {
            if (_animatorBoolHashes == null) return;
            
            var animatorBools = new bool[_animatorBoolHashes.Count]; 

            for(var i = 0; i < animatorBools.Length;i ++)
            {
                animatorBools[i] = false;
            }

            if (Animator == null) return;
            
            foreach (var t in hashes)
            {
                for (var j = 0; j < _animatorBoolHashes.Count; j++)
                {
                    if (_animatorBoolHashes[j] == t)
                    {
                        animatorBools[j] = true;
                    }
                }
            }

            
            for (var k = 0; k < _animatorBoolHashes.Count; k++)
            {
                Animator.SetBool(_animatorBoolHashes[k], animatorBools[k]);
            }
        }
        private AudioFxData SelectRandomSfxClip(List<AudioFxData> audioFxDatas)
        {
            var sumOfWeights = 0;
            foreach (var t in audioFxDatas)
            {
                sumOfWeights += t.Weight;
            }

            var randomWeight = Random.Range(1,sumOfWeights);
            
            foreach (var t in audioFxDatas)
            {
                randomWeight = randomWeight - t.Weight;
                if (randomWeight <= 0)
                {
                    return t;
                }
                
            }

            return null;
        }
        private GameObject SelectRandomPickup(List<PickUpData> pickUpDatas)
        {
            var sumOfWeights = 0;
            foreach (var t in pickUpDatas)
            {
                sumOfWeights += t.Weight;
            }

            var randomWeight = Random.Range(1,sumOfWeights);
            
            foreach (var t in pickUpDatas)
            {
                randomWeight = randomWeight - t.Weight;
                if (randomWeight <= 0)
                {
                    return t.PickUp;
                }
                
            }

            return null;
        } 
        private void PlayAudioSfx(List<AudioFxData> audioFxData)
        {
            var randomlySelectedAudioSfx = SelectRandomSfxClip(audioFxData);
            if (AudioSource == null) return;
            if (randomlySelectedAudioSfx == null) return;
            AudioSource.Stop();
            if (randomlySelectedAudioSfx.Clip == null) return;
            AudioSource.clip = randomlySelectedAudioSfx.Clip;
            AudioSource.loop = randomlySelectedAudioSfx.LoopFx;
            AudioSource.pitch = Random.Range(randomlySelectedAudioSfx.PitchMinValue, randomlySelectedAudioSfx.PitchMaxValue);
            AudioSource.volume = Random.Range(randomlySelectedAudioSfx.VolumeMinValue, randomlySelectedAudioSfx.VolumeMaxVolume);
            AudioSource.Play();
        }
        public void Handle(OnTopOfMovingPlatform message)
        {

        }
        public void Handle(OnCheckpointLoadStarted message)
        {
            Respawn();
        }
        protected virtual void Respawn()
        {
            Tf.position = SpawnPosition;
            ChangeState(UsedStates[StartingStateIndex]);
            _currentHealth = Health;
        }
        public void Handle(OnCheckpointSave message)
        {

        }
        public void Handle(OnLevelLoad message)
        {

        }
        
        private  IEnumerator Idle(StateData stateData)
        {
            StopAllStateCoroutinesExceptCurrent();
            while (true)
            {
                if (stateData.StateTransitions.Count != 0)
                {
                    if (stateData.StateTransitions.ContainsKey(EnemyStateTrigger.TimeInStateEquals.ToString()))
                    {
                        if ((stateData.TimeInState += Time.deltaTime) >= stateData.TimeToTransition)
                        {
                            ChangeState(stateData.StateTransitions[EnemyStateTrigger.TimeInStateEquals.ToString()].NewState);
                            yield break;
                        }
                        else
                        {
                            stateData.TimeInState += Time.deltaTime;
                        }
                        yield return true;
                    }
                }
                yield return true;
            }
        }//1
        private IEnumerator Patrolling(StateData stateData)
        {
            StopAllStateCoroutinesExceptCurrent();   
            while (true)
            {

                if (stateData.StateTransitions.Count != 0)
                {
                    if (stateData.StateTransitions.ContainsKey(EnemyStateTrigger.TimeInStateEquals.ToString()))
                    {
                        if ((stateData.TimeInState += Time.deltaTime) >= stateData.TimeToTransition)
                        {
                            ChangeState(stateData.StateTransitions[EnemyStateTrigger.TimeInStateEquals.ToString()].NewState);
                        }
                        else
                        {
                            stateData.TimeInState += Time.deltaTime;
                        }
                    }
                }
                yield return true;
            }
        }//2
        private IEnumerator Attacking(StateData stateData)
        {
            StopAllStateCoroutinesExceptCurrent();
            while (true)
            {
               // Messenger.Publish(new OnFireProjectile(XDirection),Tf);
                BroadcastMessage("FireProjectile",XDirection,SendMessageOptions.DontRequireReceiver);
                if (XDirection == 1)
                {
                    SetAnimatorBool(new[] {AttackingHash, RightHash});
                }
                else if (XDirection == -1)
                {
                    SetAnimatorBool(new[] {AttackingHash, LeftHash});
                }
                
                if (stateData.StateTransitions.Count != 0)
                {
                    if (stateData.StateTransitions.ContainsKey(EnemyStateTrigger.TimeInStateEquals.ToString()))
                    {
                        if ((stateData.TimeInState += Time.deltaTime) >= stateData.TimeToTransition)
                        {
                            ChangeState(stateData.StateTransitions[EnemyStateTrigger.TimeInStateEquals.ToString()]
                                .NewState);
                            yield break;
                        }
                        else
                        {
                            stateData.TimeInState += Time.deltaTime;
                        }
                    }
                }
                
                yield return new WaitForSeconds(AttackRate);
                Animator.SetBool(AttackingHash,false);
                yield return new WaitForEndOfFrame();

            }
        }//3
        private IEnumerator Charging(StateData stateData)
        {
            StopAllStateCoroutinesExceptCurrent();
            while (true)
            {
                if (stateData.StateTransitions.Count != 0)
                {
                    if (stateData.StateTransitions.ContainsKey(EnemyStateTrigger.TimeInStateEquals.ToString()))
                    {
                        if ((stateData.TimeInState += Time.deltaTime) >= stateData.TimeToTransition)
                        {
                            ChangeState(stateData.StateTransitions[EnemyStateTrigger.TimeInStateEquals.ToString()]
                                .NewState);
                            yield break;
                        }
                        else
                        {
                            stateData.TimeInState += Time.deltaTime;
                        }

                        yield return true;
                    }
                    yield return true;
                }

            }
        }//4
        private IEnumerator Stunned(StateData stateData)
        {
            StopAllStateCoroutinesExceptCurrent();
           
            while (true)
            {
                if (stateData.StateTransitions.Count != 0)
                {
                    if (stateData.StateTransitions.ContainsKey(EnemyStateTrigger.TimeInStateEquals.ToString()))
                    {
                        if ((stateData.TimeInState += Time.deltaTime) >= stateData.TimeToTransition)
                        {
                            ChangeState(stateData.StateTransitions[EnemyStateTrigger.TimeInStateEquals.ToString()]
                                .NewState);
                            yield break;
                        }
                        else
                        {
                            stateData.TimeInState += Time.deltaTime;
                        }

                        yield return true;
                    }
                }
                yield return true;
            }
        }//5
        private IEnumerator Seeking(StateData stateData)
        {
            StopAllStateCoroutinesExceptCurrent();

            while (true)
            {
                if (stateData.StateTransitions.Count != 0)
                {
                    if (stateData.StateTransitions.ContainsKey(EnemyStateTrigger.TimeInStateEquals.ToString()))
                    {
                        if ((stateData.TimeInState += Time.deltaTime) >= stateData.TimeToTransition)
                        {
                            ChangeState(stateData.StateTransitions[EnemyStateTrigger.TimeInStateEquals.ToString()].NewState);
                            yield break;
                        }
                        else
                        {
                            stateData.TimeInState += Time.deltaTime;
                        }
                        yield return true;
                    }
                }
                yield return true;
            }
        }//6
        private IEnumerator TakingDamage(StateData stateData)
        {
            StopAllStateCoroutinesExceptCurrent();

            DamageType = DamageTypes.RegularDamage;
            if(_currentHealth - 1 >= 0)
            {
                _currentHealth--;
            }
            
            
            var keys = new List<string> (stateData.StateTransitions.Keys);
            var healthTransitionValues = new List<int>();

            foreach (var key in keys)
            {
                if (stateData.StateTransitions[key].StateChangeTrigger == EnemyStateTrigger.HealthIsBelow || stateData.StateTransitions[key].StateChangeTrigger == EnemyStateTrigger.HealthIsBelowInstant )
                {
                   healthTransitionValues.Add(stateData.StateTransitions[key].HealthToTransition);
                } 
            }
           
            healthTransitionValues.Sort();

            foreach (var t in healthTransitionValues)
            {
                
                if (_currentHealth < t)
                {
                    if (stateData.StateTransitions.ContainsKey(EnemyStateTrigger.HealthIsBelow.ToString() + t))
                    {
                        if (stateData.StateTransitions[EnemyStateTrigger.HealthIsBelow.ToString() + t].NewState ==
                            "Detonating")
                        {
                            DamageType = DamageTypes.CriticalDamage;
                        }
                        _damageFlashIsFinished = false;
                        SendMessage("StartDamageFlash", this, SendMessageOptions.DontRequireReceiver);
                        yield return new WaitUntil(() => _damageFlashIsFinished);
                        ChangeState(stateData
                            .StateTransitions[EnemyStateTrigger.HealthIsBelow.ToString() + t]
                            .NewState);
                        yield break;
                    }

                    if (stateData.StateTransitions.ContainsKey(EnemyStateTrigger.HealthIsBelowInstant.ToString() + t))
                    {
                        ChangeState(stateData
                            .StateTransitions[
                                EnemyStateTrigger.HealthIsBelowInstant.ToString() + t]
                            .NewState);
                        yield break;
                    }
                }
            }

            _damageFlashIsFinished = false;
            SendMessage("StartDamageFlash",this, SendMessageOptions.DontRequireReceiver);
            yield return new WaitUntil(() => _damageFlashIsFinished);
            
            if (stateData.StateTransitions[EnemyStateTrigger.StateIsFinished.ToString()] != null)
            {
                ChangeState(stateData.StateTransitions[EnemyStateTrigger.StateIsFinished.ToString()].NewState);
            }
           
        }//7
        private IEnumerator Detonating(StateData stateData)
        {
            StopAllStateCoroutinesExceptCurrent();
            SendMessageUpwards("OnDestroyed", SendMessageOptions.DontRequireReceiver);

            if (SpawnPickupsOnDeath)
            {
                var randomPickup = SelectRandomPickup(PickUps);
                if (randomPickup != null)
                {
                   Instantiate(randomPickup,Tf.position,Quaternion.identity); 
                }
            }

            if (DeathVisualFx != null)
            {
                Instantiate(DeathVisualFx, Tf.position, Quaternion.identity);
            }


            if (AudioSource != null)
            {
                yield return new WaitUntil(() => AudioSource.isPlaying == false);
            }
            
            StopAllCoroutines();
            gameObject.SetActive(false);
            
        }//8
        private IEnumerator Jumping(StateData stateData)
        {
            StopAllStateCoroutinesExceptCurrent();

            if (CanAirJump == false)
            {
                yield return new WaitUntil(() => IsGrounded);
            }
            
            IsJumping = true;
            MoveDirection.y = JumpStrength;
            if (StateTransitionData[State.ToString()].StateTransitions
                .ContainsKey(EnemyStateTrigger.BecameAirBorne.ToString()))
            {
                ChangeState(StateTransitionData[State.ToString()]
                    .StateTransitions[EnemyStateTrigger.BecameAirBorne.ToString()].NewState);
            }
            while (true)
            {
                if (stateData.StateTransitions.Count != 0)
                {
                    if (stateData.StateTransitions.ContainsKey(EnemyStateTrigger.TimeInStateEquals.ToString()))
                    {
                        if ((stateData.TimeInState += Time.deltaTime) >= stateData.TimeToTransition)
                        {
                            ChangeState(stateData.StateTransitions[EnemyStateTrigger.TimeInStateEquals.ToString()]
                                .NewState);
                            yield break;
                        }
                        else
                        {
                            stateData.TimeInState += Time.deltaTime;
                        }

                        yield return true;
                    }
                }
                yield return true;
            }
        }//9
        private IEnumerator Sleeping(StateData stateData)
        {
            StopAllStateCoroutinesExceptCurrent();
            while (true)
            {
                if (stateData.StateTransitions.Count != 0)
                {
                    if (stateData.StateTransitions.ContainsKey(EnemyStateTrigger.TimeInStateEquals.ToString()))
                    {
                        if ((stateData.TimeInState += Time.deltaTime) >= stateData.TimeToTransition)
                        {
                            ChangeState(stateData.StateTransitions[EnemyStateTrigger.TimeInStateEquals.ToString()]
                                .NewState);
                            yield break;
                        }
                        else
                        {
                            stateData.TimeInState += Time.deltaTime;
                        }

                        yield return true;
                    }
                }
                yield return true;
            }
            yield return true;
        }//10

        private IEnumerator Falling(StateData stateData)
        {
            StopAllStateCoroutinesExceptCurrent();
            while (true)
            {
                if (stateData.StateTransitions.Count != 0)
                {
                    if (stateData.StateTransitions.ContainsKey(EnemyStateTrigger.TimeInStateEquals.ToString()))
                    {
                        if ((stateData.TimeInState += Time.deltaTime) >= stateData.TimeToTransition)
                        {
                            ChangeState(stateData.StateTransitions[EnemyStateTrigger.TimeInStateEquals.ToString()]
                                .NewState);
                            yield break;
                        }
                        else
                        {
                            stateData.TimeInState += Time.deltaTime;
                        }

                        yield return true;
                    }
                }
                yield return true;
            }
        }


        private IEnumerator UpdateAnimator()
        {
            while (true)
            {
                if (State != EnemyState.Attacking)
                {
                    if (XDirection == 1)
                    {
                        SetAnimatorBool(new[] {_activeHash, RightHash});
                    }
                    else if (XDirection == -1)
                    {
                        SetAnimatorBool(new[] {_activeHash, LeftHash});
                    }
                }

                yield return true;
            }
        }
        private IEnumerator ResetDiscHitDetection()
        {
            yield return new WaitForSeconds (0.5f);
            _canRecieveHitByDiscMessages = true;
        }

        private IEnumerator RotateToSurface(float targetAngle)
        {
            Debug.Log("Starting Rotate");
            _isRotating = true;
            XDirection *= -1;
            Tween flipRotate = Tf.DORotate(new Vector3(0,0,targetAngle), RotationSpeed, RotateMode.FastBeyond360);
            yield return flipRotate.WaitForCompletion();
            Debug.Log("Finished Rotate");
            _isRotating = false;
        }
        
        public void ChangeState(string stateToChangeTo)
        {
            if (stateToChangeTo != "")
            {
                State = (EnemyState) Enum.Parse(typeof(EnemyState), stateToChangeTo);
                PlayAudioSfx(StateTransitionData[stateToChangeTo].AudioFx);
                IgnoreLedges = StateTransitionData[stateToChangeTo].IgnoreLedges;
                IgnoreWalls = StateTransitionData[stateToChangeTo].IgnoreWalls;
                ChangeDirectionAtWalls = StateTransitionData[stateToChangeTo].ChangeDirectionAtWalls;
                UseGravity = StateTransitionData[stateToChangeTo].UseGravity;
                MovementFollowTargets = StateTransitionData[stateToChangeTo].MovementFollowTargetsTargets;
                
                if (MovementFollowTargets == FollowTargets.Player)
                {
                    MovementTargetPoint = _player;
                }
                else if (MovementFollowTargets == FollowTargets.TargetPoint)
                {
                    MovementTargetPoint = StateTransitionData[stateToChangeTo].MovementTargetPoint;
                }
                
                
                if (StateTransitionData[stateToChangeTo].ChangeDirectionToFacePlayer)
                {
                    if (_player != null)
                    {
                        if (_player.position.x < Tf.position.x)
                        {
                            XDirection = -1;
                        }
                        else if (_player.position.x > Tf.position.x)
                        {
                            XDirection = 1;
                        }

                        IsTouchingWall = false;
                    }
                }

                if (StateTransitionData[stateToChangeTo].ResetSpeedToZero)
                {
                    TargetSpeed = 0;
                    AppliedSpeed = 0;
                }
                
                StateTransitionData[stateToChangeTo].TimeInState = 0;
                
                //Weakspot Activation
                
                BroadcastMessage("DeactivateWeakspot", SendMessageOptions.DontRequireReceiver);
                _useBaseColliderAsWeakspot = false;
                
                if (StateTransitionData[stateToChangeTo].StateWeakSpot == StateData.StateWeakSpotType.Global)
                {
                    if (WeakSpot == WeakSpotType.Self)
                    {
                        _useBaseColliderAsWeakspot = true;
                    }
                    else if (WeakSpot == WeakSpotType.External)
                    {
                        for (var i = 0; i < WeakSpotTransforms.Count; i++)
                        {
                            WeakSpotTransforms[i].SendMessage("ActivateWeakspot", SendMessageOptions.DontRequireReceiver);
                        }
                    }
                }
                else if (StateTransitionData[stateToChangeTo].StateWeakSpot == StateData.StateWeakSpotType.External)
                {
                    
                    for (var i = 0; i < StateTransitionData[stateToChangeTo].WeakSpotTransforms.Count; i++)
                    {
                        StateTransitionData[stateToChangeTo].WeakSpotTransforms[i].SendMessage("ActivateWeakspot", SendMessageOptions.DontRequireReceiver);
                    }
                }
                else if (StateTransitionData[stateToChangeTo].StateWeakSpot == StateData.StateWeakSpotType.Self)
                {
                    _useBaseColliderAsWeakspot = true;
                }

                if (StateTransitionData[stateToChangeTo].SightTracking == StateData.StateSightTrackingTypes.Global)
                {
                    SightTracking = GlobalSightTracking;
                }
                else
                {
                    SightTracking = (SightSensorTrackingTypes)StateTransitionData[stateToChangeTo].SightTracking -1 ;
                }
                
                var keys = new List<string> (StateTransitionData[stateToChangeTo].StateTransitions.Keys);

                foreach (var key in keys)
                {
                    if (StateTransitionData[stateToChangeTo].StateTransitions[key].StateChangeTrigger == EnemyStateTrigger.YVelocityIsBelow)
                    {
                        YVelocityBelowTransitonValues.Add(StateTransitionData[stateToChangeTo].StateTransitions[key].YVelocityBelowToTransition);
                    } 
                    if (StateTransitionData[stateToChangeTo].StateTransitions[key].StateChangeTrigger == EnemyStateTrigger.YVelocityIsAbove)
                    {
                        YVelocityAboveTransitonValues.Add(StateTransitionData[stateToChangeTo].StateTransitions[key].YVelocityAboveToTransition);
                    } 
                    if (StateTransitionData[stateToChangeTo].StateTransitions[key].StateChangeTrigger == EnemyStateTrigger.XVelocityIsBelow)
                    {
                        XVelocityBelowTransitonValues.Add(StateTransitionData[stateToChangeTo].StateTransitions[key].XVelocityBelowToTransition);
                    } 
                    if (StateTransitionData[stateToChangeTo].StateTransitions[key].StateChangeTrigger == EnemyStateTrigger.XVelocityIsAbove)
                    {
                        XVelocityAboveTransitonValues.Add(StateTransitionData[stateToChangeTo].StateTransitions[key].XVelocityAboveToTransition);
                    } 
                }

                if (StateTransitionData[stateToChangeTo].ChangeGravitySurface)
                {
                    if (StateTransitionData[stateToChangeTo].GravitySurface == StateData.GravitySurfaces.Global)
                    {

                    }
                    else if (StateTransitionData[stateToChangeTo].GravitySurface == StateData.GravitySurfaces.Ground)
                    {
                        GravitySurface = GravitySurfaces.Ground;
                    }
                    else if (StateTransitionData[stateToChangeTo].GravitySurface == StateData.GravitySurfaces.Ceiling)
                    {
                        GravitySurface = GravitySurfaces.Ceiling;
                    }
                    else if (StateTransitionData[stateToChangeTo].GravitySurface == StateData.GravitySurfaces.LeftWall)
                    {
                        GravitySurface = GravitySurfaces.LeftWall;
                    }
                    else if (StateTransitionData[stateToChangeTo].GravitySurface == StateData.GravitySurfaces.RightWall)
                    {
                        GravitySurface = GravitySurfaces.RightWall;
                    }
                    else if (StateTransitionData[stateToChangeTo].GravitySurface == StateData.GravitySurfaces.Clockwise90)
                    {
                        if (GravitySurface == GravitySurfaces.Ground)
                        {
                            GravitySurface = GravitySurfaces.LeftWall;
                        }
                        else if (GravitySurface == GravitySurfaces.Ceiling)
                        {
                            GravitySurface = GravitySurfaces.RightWall;
                        }
                        else if (GravitySurface == GravitySurfaces.RightWall)
                        {
                            GravitySurface = GravitySurfaces.Ground;
                        }
                        else if (GravitySurface == GravitySurfaces.LeftWall)
                        {
                            GravitySurface = GravitySurfaces.Ceiling;
                        }
                    }
                    else if (StateTransitionData[stateToChangeTo].GravitySurface == StateData.GravitySurfaces.AntiClockwise90)
                    {
                        if (GravitySurface == GravitySurfaces.Ground)
                        {
                            GravitySurface = GravitySurfaces.RightWall;
                        }
                        else if (GravitySurface == GravitySurfaces.Ceiling)
                        {
                            GravitySurface = GravitySurfaces.LeftWall;
                        }
                        else if (GravitySurface == GravitySurfaces.RightWall)
                        {
                            GravitySurface = GravitySurfaces.Ceiling;
                        }
                        else if (GravitySurface == GravitySurfaces.LeftWall)
                        {
                            GravitySurface = GravitySurfaces.Ground;
                        }
                    }
                    else if (StateTransitionData[stateToChangeTo].GravitySurface == StateData.GravitySurfaces.Flip180)
                    {
                        if (GravitySurface == GravitySurfaces.Ground)
                        {
                            GravitySurface = GravitySurfaces.Ceiling;
                        }
                        else if (GravitySurface == GravitySurfaces.Ceiling)
                        {
                            GravitySurface = GravitySurfaces.Ground;
                        }
                        else if (GravitySurface == GravitySurfaces.RightWall)
                        {
                            GravitySurface = GravitySurfaces.LeftWall;
                        }
                        else if (GravitySurface == GravitySurfaces.LeftWall)
                        {
                            GravitySurface = GravitySurfaces.RightWall;
                        }
                    }

                    if (_isRotating == false)
                    {
                        if (GravitySurface == GravitySurfaces.Ground)
                        {
                            StartCoroutine(RotateToSurface(0.0f));
                        }
                        else if (GravitySurface == GravitySurfaces.Ceiling)
                        {
                            StartCoroutine(RotateToSurface(180.0f));
                        }
                        else if (GravitySurface == GravitySurfaces.LeftWall)
                        {
                            StartCoroutine(RotateToSurface(-90.0f));
                        }
                        else if (GravitySurface == GravitySurfaces.RightWall)
                        {
                            StartCoroutine(RotateToSurface(90.0f));
                        }
                    }

                    CalculateBoxCastSizes();
                }


                YVelocityBelowTransitonValues.Sort();
                YVelocityAboveTransitonValues.Sort();
                XVelocityBelowTransitonValues.Sort();
                XVelocityAboveTransitonValues.Sort();
                
                
                switch (stateToChangeTo)
                {
                    case "Patrolling":
                        _activeCoroutines[stateToChangeTo] =
                            StartCoroutine(Patrolling(StateTransitionData[stateToChangeTo]));
                        _activeHash = PatrollingHash;
                        break;
                    case "Idle":
                        _activeCoroutines[stateToChangeTo] = StartCoroutine(Idle(StateTransitionData[stateToChangeTo]));
                        _activeHash = IdleHash;
                        break;
                    case "Attacking":
                        _activeCoroutines[stateToChangeTo] =
                            StartCoroutine(Attacking(StateTransitionData[stateToChangeTo]));
                        _activeHash = AttackingHash;
                        break;
                    case "Charging":
                        _activeCoroutines[stateToChangeTo] =
                            StartCoroutine(Charging(StateTransitionData[stateToChangeTo]));
                        _activeHash = ChargingHash;
                        break;
                    case "Stunned":
                        _activeCoroutines[stateToChangeTo] =
                            StartCoroutine(Stunned(StateTransitionData[stateToChangeTo]));
                        _activeHash = StunnedHash;
                        break;
                    case "TakingDamage":
                        _activeCoroutines[stateToChangeTo] =
                            StartCoroutine(TakingDamage(StateTransitionData[stateToChangeTo]));
                        _activeHash = TakingDamageHash;
                        break;
                    case "Detonating":
                        _activeCoroutines[stateToChangeTo] =
                            StartCoroutine(Detonating(StateTransitionData[stateToChangeTo]));
                        _activeHash = DetonatingHash;
                        break;
                    case "Seeking":
                        _activeCoroutines[stateToChangeTo] =
                            StartCoroutine(Seeking(StateTransitionData[stateToChangeTo]));
                        _activeHash = SeekingHash;
                        break;
                    case "Jumping":
                        _activeCoroutines[stateToChangeTo] =
                            StartCoroutine(Jumping(StateTransitionData[stateToChangeTo]));
                        _activeHash = JumpingHash;
                        break;
                    case "Sleeping":
                        _activeCoroutines[stateToChangeTo] =
                            StartCoroutine(Sleeping(StateTransitionData[stateToChangeTo]));
                        _activeHash = SleepingHash;
                        break;
                    case "Falling":
                        _activeCoroutines[stateToChangeTo] =
                            StartCoroutine(Falling(StateTransitionData[stateToChangeTo]));
                        _activeHash = FallingHash;
                        break;
                }
            }
        }
        public void OnEnterLineOfSight(GameObject detectedObject)
        {
            if (detectedObject != null)
            {
                if (!detectedObject.CompareTag("Player")) return;
                _playerIsInLineOfSight = true;
            }
        }
        public void OnExitsLineOfSight(GameObject detectedObject)
        {
            if (detectedObject != null)
            {

                if (detectedObject.CompareTag("Player"))
                {
                    _playerIsInLineOfSight = false;
                }
            }
        }
        private void OnExitsVicinity(GameObject detectedObject)
        {
            if (detectedObject != null)
            {
                if (detectedObject.CompareTag("Player"))
                {
                    _playerIsInVicinity = false;
                }
            }
        }
        private void OnEntersVicinity(GameObject detectedObject)
        {
            if (detectedObject != null)
            {
                if (detectedObject.CompareTag("Player"))
                {
                    _playerIsInVicinity = true;
                }
            } 
        }
        private void OnCollisionEnter2D(Collision2D other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                if (StateTransitionData[State.ToString()].StateTransitions
                    .ContainsKey(EnemyStateTrigger.CollisionWithPlayer.ToString()))
                {
                    ChangeState(StateTransitionData[State.ToString()].StateTransitions[EnemyStateTrigger.CollisionWithPlayer.ToString()].NewState);
                }
            }
        }


        [Serializable]
        public class StateTransition
        {
            public EnemyStateTrigger StateChangeTrigger;
            public string[] PossibleStates;
            public int NewStateIndex;
            public string NewState = "";
            public int HealthToTransition;
            public float YVelocityBelowToTransition;
            public float YVelocityAboveToTransition;
            public float XVelocityBelowToTransition;
            public float XVelocityAboveToTransition;
        }

        [Serializable]
        public class StateData
        {
            public EnemyState StateName;
            public bool useGlobalSpeedSettings = true;
            public float Speed;
            public float Acceleration;
            public float Decceleration;
            public bool IgnoreWalls;
            public bool ChangeDirectionAtWalls = true;
            public bool IgnoreLedges;
            public bool UseGravity = true;
            public FollowTargets MovementFollowTargetsTargets;
            public Transform MovementTargetPoint;
            public enum StateWeakSpotType{Global,Self,External}
            public StateWeakSpotType StateWeakSpot;
            public bool ChangeGravitySurface = false;
            public enum GravitySurfaces{Global,Ground,Ceiling,RightWall,LeftWall,Clockwise90,AntiClockwise90,Flip180}
            public GravitySurfaces GravitySurface;
            public enum StateSightTrackingTypes{Global,FacingDirection,PlayerDirectionX,PlayerDirectionY,PlayerDirectionXY,Static}
            public StateSightTrackingTypes SightTracking;
            public List<Transform> WeakSpotTransforms = new List<Transform>();
            public List<AudioFxData> AudioFx = new List<AudioFxData>();
            public StateTransitionDictonary StateTransitions = new StateTransitionDictonary();
            public float TimeToTransition;
            public float TimeInState;
            public List<string> UnusedTransitions = new List<string>();
            public int UnusedTransitionsIndex;
            public int HealthToTransition;
            public float YVelocityBelowToTransition;
            public float YVelocityAboveToTransition;
            public float XVelocityBelowToTransition;
            public float XVelocityAboveToTransition;
            public bool ChangeDirectionToFacePlayer = false;
            public bool ResetSpeedToZero = false;
        }

        [Serializable]
        public class AudioFxData
        {
            public AudioClip Clip;
            public int Weight = 100;
            public float VolumeMinValue = 1;
            public float VolumeMaxVolume = 1;
            public float PitchMinValue = 1;
            public float PitchMaxValue = 1;
            public bool LoopFx;
        }

        [Serializable]
        public class PickUpData
        {
            public GameObject PickUp;
            public Transform PickupSpawnPoint;
            public int Weight = 100;
        }

        public void Handle(OnDamageFlashFinished message)
        {
            _damageFlashIsFinished = true;
        }

        public void Handle(OnPlayerTransformRecieved message)
        {
            if (_player == null)
            {
                _player = message.PlayerTransform;
            }
        }
    }
}
