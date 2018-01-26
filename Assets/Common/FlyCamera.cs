using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

public class FlyCamera : MonoBehaviour
{
	public float minSpeed = 0.5f;
	public float mainSpeed = 10f; // Regular speed.
	public float shiftMultiplier = 2f;  // Multiplied while shift is held.  Basically running.
	public float ctrlMultiplier = 0.5f;  // Multiplied while control is held.  Basically running.
	public float camSens = .35f;  // Camera sensitivity by mouse input.
	private Vector3 lastMouse = new Vector3(Screen.width / 2, Screen.height / 2, 0); // Kind of in the middle of the screen, rather than at the top (play).
	public bool clickToMove = true;

	void Update()
	{
		mainSpeed += Input.GetAxis("Mouse ScrollWheel") * mainSpeed;
		if (mainSpeed < minSpeed)
			mainSpeed = minSpeed;

		if (clickToMove)
		{
			if (!Input.GetMouseButton(0))
			{
				return;
			}

			if (Input.GetMouseButtonDown(0))
			{
				lastMouse = Input.mousePosition;
				return;
			}
		}

		// Mouse input.
		lastMouse = Input.mousePosition - lastMouse;
		lastMouse = new Vector3(-lastMouse.y * camSens, lastMouse.x * camSens, 0);
		lastMouse = new Vector3(transform.eulerAngles.x + lastMouse.x, transform.eulerAngles.y + lastMouse.y, 0);
		transform.eulerAngles = lastMouse;
		lastMouse = Input.mousePosition;


		// Keyboard commands.
		Vector3 p = getDirection();
		if (Input.GetKey(KeyCode.LeftShift))
			p = p * mainSpeed * shiftMultiplier;
		else if (Input.GetKey(KeyCode.LeftControl))
			p = p * mainSpeed * ctrlMultiplier;
		else
			p = p * mainSpeed;

		//p = p * Time.deltaTime;
		p = p * Time.unscaledDeltaTime;
		Vector3 newPosition = transform.position;
		if (Input.GetKey(KeyCode.V))
		{ //If player wants to move on X and Z axis only
			transform.Translate(p);
			newPosition.x = transform.position.x;
			newPosition.z = transform.position.z;
			transform.position = newPosition;
		}
		else
		{
			transform.Translate(p);
		}
	}

	private Vector3 getDirection()
	{
		Vector3 p_Velocity = new Vector3();
		if (Input.GetKey(KeyCode.W))
		{
			p_Velocity += new Vector3(0, 0, 1);
		}
		if (Input.GetKey(KeyCode.S))
		{
			p_Velocity += new Vector3(0, 0, -1);
		}
		if (Input.GetKey(KeyCode.A))
		{
			p_Velocity += new Vector3(-1, 0, 0);
		}
		if (Input.GetKey(KeyCode.D))
		{
			p_Velocity += new Vector3(1, 0, 0);
		}
		if (Input.GetKey(KeyCode.R))
		{
			p_Velocity += new Vector3(0, 1, 0);
		}
		if (Input.GetKey(KeyCode.F))
		{
			p_Velocity += new Vector3(0, -1, 0);
		}
		return p_Velocity;
	}

	public void resetRotation(Vector3 lookAt)
	{
		transform.LookAt(lookAt);
	}
}