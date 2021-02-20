// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Spaceship.cs" company="Exit Games GmbH">
//   Part of: Asteroid Demo,
// </copyright>
// <summary>
//  Spaceship
// </summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections;
using UnityEngine;
using Photon.Pun.UtilityScripts;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Photon.Pun.Demo.Asteroids
{
    public class Spaceship : MonoBehaviour
    {
        public ParticleSystem Destruction;
        public GameObject BulletPrefab;

        private PhotonView photonView;

#pragma warning disable 0109
        private new Rigidbody rigidbody;
        private new Collider collider;
        private new Renderer renderer;
#pragma warning restore 0109

        private float shootingTimer = 0.0f;

        private bool controllable = true;

        public AsteroidsGameManager gameManager;

        //Settings
        private int refreshRate = 300;
        private readonly float MOUSE_SENS_COEFF = 1f;
        private float mouseSensitivity = 0.6f;
        private float hFieldOfView = 103f;
        private const float H_FIELD_OF_VIEW_MIN = 80f;
        private const float H_FIELD_OF_VIEW_MAX = 103f;

        //Camera
        private Quaternion originalCameraRotation = Quaternion.identity;
        private Vector3 originalCameraPosition = Vector3.zero;
        public float startAngle = 0f;
        private float fpCamPitch = 0f;
        private float fpCamYaw = 0f;
        private float fpCamRoll = 0f;

        //Movement
        private readonly float SNAP = 4f; //How snappy the physics are. Multiplies both drag AND player-driven acceleration
        private readonly float DRAG = 0f; //2f; //100f;
        private readonly float DRAG_AIR_MULTIPLIER = 0f; //0.25f;
        private readonly float SPEED_HORIZONTAL_MAX_BASE = 5f; //12f;
        private readonly float THRUST_MAGNITUDE_BASE = 6e2f; //6e3f; //1.5e5f //1000f;
        private readonly float FORWARD_MULTIPLIER = 1.1f;
        private readonly float THRUST_MAGNITUDE_AIR_MULTIPLIER = 0.5f; //0.07f; //Lower value results in less control of movement while in air
        private float thrustMagnitudeMultiplier;
        private float speedHorizontalMax;
        private Vector3 thrustVector;
        private readonly float SPEED_JUMP = 8f;
        private bool jumpBindReleased = true;
        private readonly float SPEED_CLIMB = 8f;
        private bool canClimb = true;
        private bool hasClimbFatigue = false;
        private float climbDuration = 0f;
        private readonly float CLIMB_DURATION_MAX = 0.8f;
        private readonly float WALL_CLIMB_HORIZONTAL_DRAG = 16f;

        #region UNITY

        public void Awake()
        {
            photonView = GetComponent<PhotonView>();

            rigidbody = GetComponent<Rigidbody>();
            collider = GetComponent<Collider>();
            renderer = GetComponent<Renderer>();
        }

        public void Start()
        {
            //SERVER
            //Get colour for EVERY player
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                r.material.color = AsteroidsGame.GetColor(photonView.Owner.GetPlayerNumber());
            }

            //CLIENT
            if (!photonView.IsMine)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;

            //Remember spectator cam position
            originalCameraRotation = Camera.main.transform.rotation;
            originalCameraPosition = Camera.main.transform.position;

            LoadSettings();

            //Look at centre of map
            fpCamYaw = LoopEulerAngle(startAngle + 180);
        }

        private void LoadSettings()
        {
            //VSync off
            QualitySettings.vSyncCount = 0;

            //Refresh rate
            Time.fixedDeltaTime = 1f / refreshRate;
            Application.targetFrameRate = refreshRate;

            //Camera HFOV
            if (hFieldOfView > H_FIELD_OF_VIEW_MIN && hFieldOfView < H_FIELD_OF_VIEW_MAX)
            {
                //fieldOfView takes VERTICAL field of view as input, so we convert h to v
                Camera.main.fieldOfView = Camera.HorizontalToVerticalFieldOfView(hFieldOfView, Camera.main.GetComponent<Camera>().aspect);
            }

            //Clip planes
            Camera.main.nearClipPlane = 0.05f;
            Camera.main.farClipPlane = 500f;
        }

        public void Update()
        {
            if (!photonView.IsMine || !controllable)
            {
                return;
            }

            UpdateCameraPosition();
            UpdateCameraRotation();
        }

        public void FixedUpdate()
        {
            if (!photonView.IsMine)
            {
                return;
            }

            if (!controllable)
            {
                Camera.main.transform.rotation = originalCameraRotation;
                Camera.main.transform.position = originalCameraPosition;
                return;
            }

            /*
            Quaternion rot = rigidbody.rotation * Quaternion.Euler(0, rotation * RotationSpeed * Time.fixedDeltaTime, 0);
            rigidbody.MoveRotation(rot);

            Vector3 force = (rot * Vector3.forward) * acceleration * 1000.0f * MovementSpeed * Time.fixedDeltaTime;
            rigidbody.AddForce(force);

            if (rigidbody.velocity.magnitude > (MaxSpeed * 1000.0f))
            {
                rigidbody.velocity = rigidbody.velocity.normalized * MaxSpeed * 1000.0f;
            }
            */
            
            UpdateBodyRotation();
            UpdateMovement();
            
            UpdateShooting();

            CheckIfFellToDeath();

            CheckExitScreen();
        }

        private void UpdateCameraPosition()
        {
            Camera.main.transform.position = rigidbody.position + (Vector3.up * 0.5f);
        }

        private void UpdateBodyRotation()
        {
            Quaternion rotationTarget = Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up);
            //rb.rotation = rotationTarget;
            rigidbody.rotation = Quaternion.Euler(0f, rotationTarget.eulerAngles.y, 0f);
        }

        private void UpdateMovement()
        {
            bool isGrounded = MovementGetIsGrounded();

            MovementUpdateJump(isGrounded);

            MovementUpdateClimb(isGrounded);

            //MovementApplyHorizontalDrag(isGrounded);

            //Gravity
            rigidbody.AddForce(Physics.gravity);

            //PICK HORIZONTAL DIRECTION
            //Reset
            thrustVector = Vector3.zero;
            thrustMagnitudeMultiplier = 1f;
            speedHorizontalMax = SPEED_HORIZONTAL_MAX_BASE;

            //Faster if moving forward
            if (Input.GetKey(KeyCode.W))
            {
                //Add forward to thrust vector
                thrustVector += transform.forward;

                //Total multiplier
                thrustMagnitudeMultiplier *= FORWARD_MULTIPLIER;
                speedHorizontalMax *= FORWARD_MULTIPLIER;
            }

            if (Input.GetKey(KeyCode.S)) thrustVector += -transform.forward;
            if (Input.GetKey(KeyCode.A)) thrustVector += -transform.right;
            if (Input.GetKey(KeyCode.D)) thrustVector += transform.right;

            //APPLY FORCE OF TYPE
            //Test
            if (Input.GetKey(KeyCode.G))
            {
                rigidbody.AddForce(transform.forward * 200f * SNAP * Time.fixedDeltaTime);
            }

            if (thrustVector == Vector3.zero && isGrounded)
            {
                //Counter movement
                Vector3 velocityInitial = new Vector3(rigidbody.velocity.x, 0f, rigidbody.velocity.z);

                Vector3 velocityIntialDirection = -velocityInitial.normalized;
                //If counter movement direction is forward-ish
                if (Vector3.Dot(velocityIntialDirection, transform.forward) > 0.5f)
                {
                    thrustMagnitudeMultiplier *= FORWARD_MULTIPLIER;
                }

                //F = (v/t)*m
                float forceMagnitudeToBringVelocityToZero = ((velocityInitial.magnitude / Time.fixedDeltaTime) * rigidbody.mass);

                float appliedForce = Mathf.Min(GetMaxThrust(), forceMagnitudeToBringVelocityToZero);

                //Debug.Log("f0: " + forceMagnitudeToBringVelocityToZero + " | fa: " + appliedForce);

                //Clamp to limit maximum force, apply in opposite direction of current velocity
                rigidbody.AddForce(appliedForce * velocityIntialDirection);
            }
            else
            {
                if (!isGrounded)
                {
                    //Aerial movement
                    thrustMagnitudeMultiplier *= THRUST_MAGNITUDE_AIR_MULTIPLIER;
                }

                //General movement
                Vector3 thrustDirection = thrustVector.normalized;
                float thrustMagnitude = GetMaxThrust();
                Vector3 thrust = thrustDirection * thrustMagnitude;

                //Limit accelerating past max speed
                //TODO: Make counter movement for going from forward to diagonal-forward, use dot product?
                //float projectedSpeed = GetVectorProjection(thrustDirection);
                Vector3 velocityHorizontalCurrent = new Vector3(rigidbody.velocity.x, 0f, rigidbody.velocity.z);
                float projectedSpeedPlusThrust = Vector3.Project(velocityHorizontalCurrent + thrust, velocityHorizontalCurrent).magnitude;

                Debug.Log("p+t: " + projectedSpeedPlusThrust);

                /*
                if (projectedSpeed > 1f)
                {
                    thrustMagnitude -= Mathf.Max(0f, thrustMagnitude * (projectedSpeed / speedHorizontalMax));
                }
                */

                //Debug.Log("m: " + thrustMagnitude);

                if (projectedSpeedPlusThrust > speedHorizontalMax)
                {
                    //Thrust is at or near limit; reduce thrust to safely approach limit

                    float projectedSpeedCurrent = Vector3.Project(velocityHorizontalCurrent, velocityHorizontalCurrent).magnitude;
                    float maxDeltaSpeed = Mathf.Max(Time.fixedDeltaTime, speedHorizontalMax - projectedSpeedCurrent);
                    float thrustMagnitudeToMaxDeltaSpeed;
                    if (maxDeltaSpeed < 1e-2f)
                    {
                        //Don't thrust to infinity
                        thrustMagnitudeToMaxDeltaSpeed = 0f;
                        Debug.Log("limit. (" + maxDeltaSpeed + ")");
                    }
                    else
                    {
                        //F = (v/t)*m
                        thrustMagnitudeToMaxDeltaSpeed = (maxDeltaSpeed / Time.fixedDeltaTime) * rigidbody.mass;
                        Debug.Log("delta: " + thrustMagnitudeToMaxDeltaSpeed);
                    }

                    float appliedThrust = Mathf.Min(GetMaxThrust(), thrustMagnitudeToMaxDeltaSpeed);

                    Debug.Log("at: " + appliedThrust);

                    thrust = appliedThrust * thrustDirection;
                    //thrust *= Mathf.Max(0f, speedHorizontalMax - projectedSpeed); //<- this will always be less than 0
                }

                Debug.Log("t: " + thrust.magnitude);

                rigidbody.AddForce(thrust);
            }

            Debug.Log("hv: " + new Vector3(rigidbody.velocity.x, 0f, rigidbody.velocity.z).magnitude);

            //if (thrustVector == Vector3.zero && isGrounded)
            //{
            //    //Counter movement when not moving
            //
            //    Vector3 velocityInitial = new Vector3(rigidbody.velocity.x, 0f, rigidbody.velocity.z);
            //    //Vector3 velocityInitialDirection = velocityInitial.normalized;
            //    //thrustDirection = -velocityInitialDirection;
            //
            //    /*
            //     * a = v/t
            //     * F = ma
            //     * F/m = a
            //     * F/m = v/t
            //     * F = (v/t)*m
            //     */
            //
            //    Vector3 forceToBringVelocityToZero = -((velocityInitial / Time.fixedDeltaTime) * rigidbody.mass);
            //
            //
            //
            //    //Vector3 velocityDirectionFinal = ((GetMovementThrust(thrustDirection) / rigidbody.mass) * Time.fixedDeltaTime).normalized;
            //
            //    /*
            //    Vector3 velocityDirectionInitial = new Vector3(rigidbody.velocity.x, 0f, rigidbody.velocity.z).normalized;
            //    thrustDirection = -velocityDirectionInitial;
            //    */
            //
            //    /*
            //     * F = ma
            //     * a = v/t = F/m
            //     * v = at = (F/m)t
            //     */
            //    /*
            //    Vector3 velocityDirectionFinal = ((GetMovementThrust(thrustDirection) / rigidbody.mass) * Time.fixedDeltaTime).normalized;
            //
            //    //Debug.Log(velocityDirectionInitial);
            //    //Debug.Log(velocityDirectionFinal);
            //    Debug.Log(Vector3.Dot(velocityDirectionInitial, velocityDirectionFinal));
            //
            //    //If vectors not in same direction
            //    if (Vector3.Dot(velocityDirectionInitial, velocityDirectionFinal) > 0f)
            //    {
            //        thrustMultiplier = 0f;
            //    }
            //    */
            //
            //    //Add counter movement force
            //    rigidbody.AddForce(forceToBringVelocityToZero);
            //
            //    /*
            //    //Reduce vertical velocity (since we have confirmed that we're on the ground already)
            //    //todo: this erroneously overrides jumping
            //    if (rigidbody.velocity.y != 0f)
            //    {
            //        rigidbody.velocity = new Vector3(rigidbody.velocity.x, rigidbody.velocity.y / 2f, rigidbody.velocity.z);
            //    }
            //    */
            //}
            //else
            //{
            //    thrustDirection = thrustVector.normalized;
            //
            //
            //    if (isGrounded)
            //    {
            //        //Apply whatever force will bring the velocity to the desired values
            //
            //        /*
            //         * a = v/t
            //         * F = ma
            //         * F/m = a
            //         * F/m = v/t
            //         * F = (v/t)*m
            //         */
            //
            //        //Vector3 desiredForce = ((thrustDirection * SPEED_HORIZONTAL_MAX_THRUSTABLE) / Time.fixedDeltaTime) * rigidbody.mass;
            //
            //        //rigidbody.AddForce(desiredForce);
            //
            //        Vector3 velocityHorizontalNew = thrustDirection * SPEED_HORIZONTAL_MAX_THRUSTABLE;
            //        Vector3 velocityTotalNew = new Vector3(velocityHorizontalNew.x, rigidbody.velocity.y, velocityHorizontalNew.z);
            //        rigidbody.velocity = velocityTotalNew;
            //
            //        //rigidbody.AddForce(thrustDirection * SPEED_HORIZONTAL_MAX_THRUSTABLE * thrustMultiplier * Time.fixedDeltaTime, ForceMode.VelocityChange);
            //    }
            //    else
            //    {
            //        //Aerial movement
            //
            //        //Limit accelerating past max speed
            //        //TODO: Make counter movement for going from forward to diagonal-forward
            //        float sProjection = GetSignedVector3Projection(thrustDirection);
            //
            //        float totalModifier = THRUST_MAGNITUDE * thrustMultiplier;
            //        if (sProjection > 1f)
            //        {
            //            totalModifier = Mathf.Max(0f, SPEED_HORIZONTAL_MAX_THRUSTABLE - (sProjection / SPEED_HORIZONTAL_MAX_THRUSTABLE));
            //        }
            //
            //        //Aerial control
            //        //thrustMultiplier *= THRUST_MAGNITUDE_AIR_MULTIPLIER;
            //
            //        rigidbody.AddForce(thrustDirection * totalModifier * Time.fixedDeltaTime);
            //    }
            //
            //    
            //    /*
            //    //Limit accelerating past max speed
            //    //TODO: Make counter movement for going from forward to diagonal-forward
            //    sProjection = GetSignedVector3Projection(thrustDirection);
            //
            //    if (sProjection > 1f)
            //    {
            //        thrustMultiplier = Mathf.Max(0f, thrustMultiplier - (thrustMultiplier * (sProjection / SPEED_HORIZONTAL_MAX_THRUSTABLE)));
            //    }
            //
            //    //Generate and add force vector
            //    //rb.AddForce(thrustVector.normalized * THRUST_MAGNITUDE * thrustMultiplier * Time.fixedDeltaTime);
            //    //Vector3 thrust = thrustDirection * THRUST_MAGNITUDE * thrustMultiplier * Time.fixedDeltaTime;
            //    rigidbody.AddForce(thrustDirection * THRUST_MAGNITUDE * thrustMultiplier * Time.fixedDeltaTime);
            //    */
            //}
            //
            ///*
            //Debug.Log(
            //    "sProj: " + sProjection.ToString("F2")
            //    + " | mult: " + thrustMultiplier.ToString("F2")
            //    + " | speed: " + rigidbody.velocity.magnitude.ToString("F2")
            //);
            //*/
        }

        private float GetMaxThrust()
        {
            return THRUST_MAGNITUDE_BASE * SNAP * thrustMagnitudeMultiplier * Time.fixedDeltaTime;
        }

        private bool MovementGetIsGrounded()
        {
            //Get isGrounded
            float groundCheckHeight = collider.bounds.extents.y;
            float groundCheckBaseDiameter = collider.bounds.extents.x;
            float groundCheckOffsetMain = 0.05f;

            Vector3 groundCheckPositionMain = transform.position - (Vector3.up * ((groundCheckHeight - groundCheckBaseDiameter) + groundCheckOffsetMain));

            bool isGrounded = false;
            Collider[] groundCheckHitColliders = Physics.OverlapSphere(groundCheckPositionMain, groundCheckBaseDiameter / 1.1f);
            foreach (Collider groundCheckHitCollider in groundCheckHitColliders)
            {
                if (groundCheckHitCollider != collider)
                {
                    isGrounded = true;
                }
            }

            return isGrounded;
        }

        private void MovementUpdateJump(bool isGrounded)
        {
            if (Input.GetKeyUp(KeyCode.Space))
            {
                jumpBindReleased = true;
            }
            if (isGrounded && Input.GetKey(KeyCode.Space) && jumpBindReleased)
            {
                jumpBindReleased = false;
                rigidbody.velocity = new Vector3(rigidbody.velocity.x, SPEED_JUMP, rigidbody.velocity.z);
                //rb.AddForce(Vector3.up * THRUST_MAGNITUDE_JUMP * fixedUpdateRate);
            }
        }

        private void MovementUpdateClimb(bool isGrounded)
        {
            //Get isAgainstWall
            float wallCheckHeight = collider.bounds.extents.y;
            float wallCheckBaseDiameter = collider.bounds.extents.x;
            //float wallCheckOffsetMain = 0.05f;

            Vector3 wallCheckPositionMain = transform.position - (transform.up * (wallCheckHeight / 3f)) + (transform.forward * wallCheckBaseDiameter);

            bool isAgainstWall = false;
            Collider[] wallCheckHitColliders = Physics.OverlapSphere(wallCheckPositionMain, wallCheckBaseDiameter / 1.1f);
            foreach (Collider wallCheckHitCollider in wallCheckHitColliders)
            {
                if (wallCheckHitCollider != collider)
                {
                    isAgainstWall = true;
                }
            }

            //Get canClimb
            if (isGrounded)
            {
                canClimb = true;
                hasClimbFatigue = false;
                climbDuration = 0f;
            }

            //Climb
            if (isAgainstWall && canClimb && Input.GetKey(KeyCode.Space) && climbDuration <= CLIMB_DURATION_MAX)
            {
                rigidbody.velocity = new Vector3(
                    rigidbody.velocity.x * (1f - (WALL_CLIMB_HORIZONTAL_DRAG * SNAP * Time.fixedDeltaTime)),
                    SPEED_CLIMB,
                    rigidbody.velocity.z * (1f - (WALL_CLIMB_HORIZONTAL_DRAG * SNAP * Time.fixedDeltaTime))
                );
                //rigidbody.AddForce(Vector3.up * THRUST_MAGNITUDE_CLIMB * fixedUpdateRate);

                climbDuration += Time.fixedDeltaTime;
                Debug.Log(climbDuration + "/" + CLIMB_DURATION_MAX);

                hasClimbFatigue = true;
            }
            else if (hasClimbFatigue)
            {
                canClimb = false;
            }
        }

        private void MovementApplyHorizontalDrag(bool isGrounded)
        {
            //Init horizontal drag
            Vector3 horizontalVelocity = new Vector3(rigidbody.velocity.x, 0f, rigidbody.velocity.z);

            if (isGrounded)
            {
                //Horizontal drag
                horizontalVelocity *= Mathf.Max(0f, 1f - (DRAG * SNAP * Time.fixedDeltaTime));
            }
            else
            {
                //Aerial horizontal drag
                horizontalVelocity *= Mathf.Max(0f, 1f - (DRAG * DRAG_AIR_MULTIPLIER * SNAP * Time.fixedDeltaTime));
                //horizontalVelocity *= Mathf.Max(0f, 1f - (DRAG * AIR_CONTROL_MULTIPLIER * Time.fixedDeltaTime));
            }

            //Apply horizontal drag
            rigidbody.velocity = new Vector3(horizontalVelocity.x, rigidbody.velocity.y, horizontalVelocity.z);
        }

        private void UpdateCameraRotation()
        {
            //SET ROTATIONAL AXES TO MOUSE INPUT
            //Debug.LogFormat("Pitch {0}, Yaw {1}", fpCamPitch, fpCamYaw);

            //Pitch
            fpCamPitch -= Input.GetAxisRaw("Mouse Y") * mouseSensitivity * MOUSE_SENS_COEFF;
            //Yaw
            if (fpCamPitch >= 90 && fpCamPitch < 270)
            {
                //Normal
                fpCamYaw -= Input.GetAxisRaw("Mouse X") * mouseSensitivity * MOUSE_SENS_COEFF;
            }
            else
            {
                //Inverted
                fpCamYaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity * MOUSE_SENS_COEFF;
            }
            //Roll
            fpCamRoll = 0f;

            //LOOP ANGLE
            LoopEulerAngle(fpCamYaw);
            LoopEulerAngle(fpCamPitch);
            LoopEulerAngle(fpCamRoll);

            //CLAMP ANGLE
            fpCamPitch = Mathf.Clamp(fpCamPitch, -90f, 89f);

            //APPLY ROTATION
            Camera.main.transform.localRotation = Quaternion.Euler(fpCamPitch, fpCamYaw, 0f);

            //Debug.Log(fpCamYaw);
        }

        private void UpdateShooting()
        {
            if (Input.GetMouseButtonDown(0) && shootingTimer <= 0.0)
            {
                shootingTimer = 0.2f;

                photonView.RPC("Fire", RpcTarget.AllViaServer, rigidbody.position, rigidbody.rotation);
            }

            if (shootingTimer > 0.0f)
            {
                shootingTimer -= Time.deltaTime;
            }
        }

        private void CheckIfFellToDeath()
        {
            if (transform.position.y < -30)
            {
                Debug.Log(PhotonNetwork.LocalPlayer.NickName + " fell out of the world");
                DestroySpaceship();
            }
        }
        
        #endregion

        #region COROUTINES

        private IEnumerator WaitForRespawn()
        {
            yield return new WaitForSeconds(AsteroidsGame.PLAYER_RESPAWN_TIME);

            photonView.RPC("RespawnSpaceship", RpcTarget.AllViaServer);
        }

        public void OnCollisionEnter(Collision collision)
        {
            //Shot by bullet
            if (collision.gameObject.CompareTag("Bullet"))
            {
                if (photonView.IsMine)
                {
                    Bullet bullet = collision.gameObject.GetComponent<Bullet>();

                    if (bullet.Owner != PhotonNetwork.LocalPlayer)
                    {
                        bullet.Owner.AddScore(1);
                        Debug.Log(PhotonNetwork.LocalPlayer.NickName + " was killed by " + bullet.Owner.NickName);
                        DestroySpaceship();
                    }
                    else
                    {
                        /*
                        bullet.Owner.SetScore(bullet.Owner.GetScore() - 1);
                        Debug.Log(PhotonNetwork.LocalPlayer.NickName + "killed themselves");
                        DestroySpaceship();
                        */
                    }
                }
            }
        }

        #endregion

        #region PUN CALLBACKS

        [PunRPC]
        public void DestroySpaceship()
        {
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            transform.position = Vector3.zero;

            collider.enabled = false;
            renderer.enabled = false;

            controllable = false;

            Destruction.Play();

            if (photonView.IsMine)
            {
                if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(AsteroidsGame.PLAYER_LIVES, out object lives))
                {
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable {{AsteroidsGame.PLAYER_LIVES, ((int) lives <= 1) ? 0 : ((int) lives - 1)}});

                    if (((int) lives) > 1)
                    {
                        StartCoroutine("WaitForRespawn");
                    }
                }
            }
        }

        [PunRPC]
        public void Fire(Vector3 position, Quaternion rotation, PhotonMessageInfo info)
        {
            float lag = (float) (PhotonNetwork.Time - info.SentServerTime);
            GameObject bullet;

            /** Use this if you want to fire one bullet at a time **/
            bullet = Instantiate(BulletPrefab, position, Quaternion.identity);
            bullet.GetComponent<Bullet>().InitializeBullet(photonView.Owner, (rotation * Vector3.forward), Mathf.Abs(lag));


            /** Use this if you want to fire two bullets at once **/
            //Vector3 baseX = rotation * Vector3.right;
            //Vector3 baseZ = rotation * Vector3.forward;

            //Vector3 offsetLeft = -1.5f * baseX - 0.5f * baseZ;
            //Vector3 offsetRight = 1.5f * baseX - 0.5f * baseZ;

            //bullet = Instantiate(BulletPrefab, rigidbody.position + offsetLeft, Quaternion.identity) as GameObject;
            //bullet.GetComponent<Bullet>().InitializeBullet(photonView.Owner, baseZ, Mathf.Abs(lag));
            //bullet = Instantiate(BulletPrefab, rigidbody.position + offsetRight, Quaternion.identity) as GameObject;
            //bullet.GetComponent<Bullet>().InitializeBullet(photonView.Owner, baseZ, Mathf.Abs(lag));
        }

        [PunRPC]
        public void RespawnSpaceship()
        {
            collider.enabled = true;
            renderer.enabled = true;

            controllable = true;

            Destruction.Stop();
        }
        
        #endregion

        private void CheckExitScreen()
        {
            if (Camera.main == null)
            {
                return;
            }
            
            if (Mathf.Abs(rigidbody.position.x) > (Camera.main.orthographicSize * Camera.main.aspect))
            {
                rigidbody.position = new Vector3(-Mathf.Sign(rigidbody.position.x) * Camera.main.orthographicSize * Camera.main.aspect, 0, rigidbody.position.z);
                rigidbody.position -= rigidbody.position.normalized * 0.1f; // offset a little bit to avoid looping back & forth between the 2 edges 
            }

            if (Mathf.Abs(rigidbody.position.z) > Camera.main.orthographicSize)
            {
                rigidbody.position = new Vector3(rigidbody.position.x, rigidbody.position.y, -Mathf.Sign(rigidbody.position.z) * Camera.main.orthographicSize);
                rigidbody.position -= rigidbody.position.normalized * 0.1f; // offset a little bit to avoid looping back & forth between the 2 edges 
            }
        }

        private float LoopEulerAngle(float angle)
        {
            if (angle >= 360) angle -= 360;
            else if (angle < 0) angle += 360;

            return angle;
        }


        private float GetVectorProjection(Vector3 direction)
        {
            //Calculate SIGNED projection (speed along a vector)
            //Code by Parappa (modified by Reason): https://answers.unity.com/questions/767962/want-velocity-along-a-direction.html

            if (direction == Vector3.zero)
            {
                return 0f;
            }
            else
            {
                // you have to reverse the x axis for some reason
                Vector3 velocity = new Vector3(-rigidbody.velocity.x, rigidbody.velocity.y, rigidbody.velocity.z);
                Vector3 rotatedVelocity = Quaternion.LookRotation(direction) * velocity;
                float directionSpeed = rotatedVelocity.z + rigidbody.velocity.magnitude; //add current velocity so that diagonal projections are accurate

                return directionSpeed;
            }
            

            //Debug.Log(directionSpeed);


            //Trying to prevent being able to gain massive speed just by jumping because there's zero friction in the air
            //Trying to make thrust proportional to speed in direction trying to thrust in
            //So if you're already going fast in that direction, you won't accelerate by much (but you won't slow down via drag either)
            //The other goal is to retain manoeuvrability in the air, so if you want to go backwards while go max speed forwards, you should still get max thrust going backwards

            //Vector3 projection = Vector3.Project(rigidbody.velocity, Vector3.forward);
            //Debug.Log(projection.magnitude);


            //Vector3 projection = Vector3.Project(rigidbody.velocity, thrustDirection);
            //float projectionSpeed = (rigidbody.velocity + projection).magnitude;
            //thrustMultiplier *= Mathf.Min(1f, (1f / projectionSpeed) * THRUST_FORWARD_INERTIA);
            //Debug.Log(projection + "\nrb\n" + rigidbody.velocity);
        }
    }
}