using UnityEngine;

namespace Inspection
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        public Camera viewCamera;
        public InspectionHud hud;
        public float speed = 3.5f;
        public float sensitivity = 2f;
        private CharacterController controller;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float pitch;
        private float verticalSpeed;
        private bool looking;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void Update()
        {
            if (hud.BlocksSceneInput) { ReleasePointer(); return; }
            if (Input.GetKeyDown(KeyCode.R)) ResetView();
            if (Input.GetKeyDown(KeyCode.Escape)) ReleasePointer();
            if (Input.GetMouseButtonDown(1) && !hud.ContainsScreenPoint(Input.mousePosition))
            {
                looking = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (Input.GetMouseButtonUp(1)) ReleasePointer();
            if (looking)
            {
                transform.Rotate(0f, Input.GetAxis("Mouse X") * sensitivity, 0f);
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivity, -75f, 75f);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            Vector3 movement = transform.TransformDirection(Vector3.ClampMagnitude(input, 1f)) * speed;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            movement.y = verticalSpeed;
            controller.Move(movement * Time.deltaTime);
        }

        public void ResetView()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            viewCamera.transform.localRotation = Quaternion.identity;
            pitch = 0f;
            verticalSpeed = 0f;
            controller.enabled = true;
            ReleasePointer();
        }

        public void ReleasePointer()
        {
            looking = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnApplicationFocus(bool focus) { if (!focus) ReleasePointer(); }
        private void OnDisable() { ReleasePointer(); }
    }
}
