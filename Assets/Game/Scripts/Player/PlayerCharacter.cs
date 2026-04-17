using System;
using Game.Scripts.Core;
using KinematicCharacterController;
using UnityEngine;

namespace Game.Scripts.Player {
    public enum Stance {
        STAND, CROUCH, SPRINT
    }

    public struct CharacterInput {
        public Quaternion Rotation;
        public Vector2 Move;
        public bool Jump;
        public bool Crouch;
        public bool Sprint;
    }

    public class PlayerCharacter : MonoBehaviour, ICharacterController {
        [SerializeField] private KinematicCharacterMotor motor;
        [SerializeField] private Transform root;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private LayerMask collidableLayers;
        [Space]
        [SerializeField] private float walkSpeed = 10.0f;
        [SerializeField] private float sprintSpeed = 15.0f;
        [SerializeField] private float crouchSpeed = 5.0f;
        [SerializeField] private float walkResponse = 15.0f;
        [SerializeField] private float sprintResponse = 20.0f;
        [SerializeField] private float crouchResponse = 10.0f;
        [Space]
        [SerializeField] private float airSpeed = 8.0f;
        [SerializeField] private float airSprintSpeed = 13.0f;
        [SerializeField] private float airAcceleration = 70.0f;
        [Space]
        [SerializeField] private float jumpSpeed = 10.0f;
        [SerializeField] private float gravity = -50.0f;
        [Space]
        [SerializeField] private float standHeight = 2.0f;
        [SerializeField] private float crouchHeight = 1.4f;
        [SerializeField] private float crouchHeightResponse = 15.0f;
        [Range(0.0f, 1.0f)] [SerializeField] private float standCameraTargetHeight = 0.9f;
        [Range(0.0f, 1.0f)] [SerializeField] private float crouchCameraTargetHeight = 0.75f;

        private Stance stance;
        private bool isCrouched; // actual crouch state

        private Quaternion requestedRotation;
        private Vector3 requestedMovement;
        private bool requestedJump;
        private bool requestedCrouch;
        private bool requestedSprint;

        private Collider[] uncrouchOverlapResults;

        private Vector3 pausedVelocity;
        private bool wasPaused = false;

        public void Initialize() {
            stance = Stance.STAND;
            isCrouched = false;
            motor.CharacterController = this;
            motor.SetCapsuleDimensions(radius: motor.Capsule.radius, height: standHeight, yOffset: standHeight * 0.5f);
            uncrouchOverlapResults = new Collider[8];
        }

        public void UpdateBody(float deltaTime) {
            var currentHeight = motor.Capsule.height;
            var normalizedHeight = currentHeight / standHeight;

            var cameraTargetHeight = currentHeight * (isCrouched ? crouchCameraTargetHeight : standCameraTargetHeight);
            var rootTargetScale = new Vector3(1.0f, normalizedHeight, 1.0f);

            cameraTarget.localPosition = Vector3.Lerp(
                a: cameraTarget.localPosition,
                b: new Vector3(0.0f, cameraTargetHeight, 0.0f),
                t: 1.0f - Mathf.Exp(-crouchHeightResponse * deltaTime)
            );

            root.localScale = Vector3.Lerp(
                a: root.localScale,
                b: rootTargetScale,
                t: 1.0f - Mathf.Exp(-crouchHeightResponse * deltaTime)
            );
        }

        public void UpdateInput(CharacterInput input) {
            requestedRotation = input.Rotation;

            requestedMovement = new Vector3(input.Move.x, 0.0f, input.Move.y);
            requestedMovement = Vector3.ClampMagnitude(requestedMovement, 1);
            requestedMovement = Quaternion.Euler(0, input.Rotation.eulerAngles.y, 0) * requestedMovement;

            requestedJump = requestedJump || input.Jump;
            requestedCrouch = input.Crouch;
            requestedSprint = input.Sprint && !requestedCrouch;

            if (isCrouched)
                stance = Stance.CROUCH;
            else if (requestedSprint)
                stance = Stance.SPRINT;
            else
                stance = Stance.STAND;
        }

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime) {
            if (GameManager.instance?.GetGameState() != GameState.PLAYING) return;

            var forward = Vector3.ProjectOnPlane(
                requestedRotation * Vector3.forward,
                motor.CharacterUp
            );

            if (forward.sqrMagnitude > 0.01f)
                currentRotation = Quaternion.LookRotation(forward, motor.CharacterUp);
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime) {
            bool isPaused = GameManager.instance?.GetGameState() == GameState.PAUSED;

            if (isPaused) {
                if (!wasPaused)
                    pausedVelocity = currentVelocity;

                currentVelocity = Vector3.zero;
                wasPaused = true;
                return;
            }

            if (wasPaused && !isPaused) {
                currentVelocity = pausedVelocity;
                wasPaused = false;
            }

            if (motor.GroundingStatus.IsStableOnGround) {
                var groundedMovement = motor.GetDirectionTangentToSurface(
                    direction: requestedMovement,
                    surfaceNormal: motor.GroundingStatus.GroundNormal
                ) * requestedMovement.magnitude;

                var speed = stance switch {
                    Stance.STAND  => walkSpeed,
                    Stance.SPRINT => sprintSpeed,
                    Stance.CROUCH => crouchSpeed,
                    _             => walkSpeed
                };
                var response = stance is Stance.STAND or Stance.CROUCH ? walkResponse : crouchResponse;

                var targetVelocity = groundedMovement * speed;
                currentVelocity = Vector3.Lerp(
                    a: currentVelocity,
                    b: targetVelocity,
                    t: 1.0f - Mathf.Exp(-response * deltaTime)
                );
            } else {
                var currentAirSpeed = stance == Stance.SPRINT ? airSprintSpeed : airSpeed;

                if (requestedMovement.sqrMagnitude > 0.0f) {
                    var planarMovement = Vector3.Normalize(
                        Vector3.ProjectOnPlane(
                            vector: requestedMovement,
                            planeNormal: motor.CharacterUp
                        )
                    ) * requestedMovement.magnitude;

                    var currentPlanarVelocity = Vector3.ProjectOnPlane(
                        vector: currentVelocity,
                        planeNormal: motor.CharacterUp
                    );

                    var movementForce = planarMovement * (airAcceleration * deltaTime);
                    var targetPlanarVelocity = currentPlanarVelocity + movementForce;
                    targetPlanarVelocity = Vector3.ClampMagnitude(targetPlanarVelocity, currentAirSpeed);

                    currentVelocity += targetPlanarVelocity - currentPlanarVelocity;
                }

                currentVelocity += motor.CharacterUp * (gravity * deltaTime);
            }

            if (requestedJump) {
                requestedJump = false;

                if (motor.GroundingStatus.IsStableOnGround) {
                    motor.ForceUnground(time: 0.0f);

                    var currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
                    var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
                    currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
                }
            }
        }

        public void BeforeCharacterUpdate(float deltaTime) {
            if (requestedCrouch && !isCrouched) {
                motor.SetCapsuleDimensions(radius: motor.Capsule.radius, height: crouchHeight, yOffset: crouchHeight * 0.5f);
                isCrouched = true;
            }
        }

        public void AfterCharacterUpdate(float deltaTime) {
            if (!requestedCrouch && isCrouched) {
                motor.SetCapsuleDimensions(radius: motor.Capsule.radius, height: standHeight, yOffset: standHeight * 0.5f);
                var pos = motor.TransientPosition;
                var rot = motor.TransientRotation;

                if (motor.CharacterOverlap(pos, rot, uncrouchOverlapResults, collidableLayers, QueryTriggerInteraction.Ignore) > 0) {
                    motor.SetCapsuleDimensions(radius: motor.Capsule.radius, height: crouchHeight, yOffset: crouchHeight * 0.5f);
                } else {
                    isCrouched = false;
                }
            }
        }
        
        public void PostGroundingUpdate(float deltaTime) { }
        
        public bool IsColliderValidForCollisions(Collider coll) => true;

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }

        public void OnDiscreteCollisionDetected(Collider hitCollider) { }

        public Transform GetCameraTarget() => cameraTarget;
    }
}