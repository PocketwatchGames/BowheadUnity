using UnityEngine;
using System.Collections;
namespace EnviroSamples
{
public class FPSController : MonoBehaviour {

	public float speed = 2f;
	public float sensitivity = 2f;
	CharacterController player;

	public GameObject eyes;

	float moveFB;
	float moveLR;

	float rotX;
	float rotY;

	// Use this for initialization
	void Start () {

		player = GetComponent<CharacterController> ();

	}

	// Update is called once per frame
	void Update () {

		moveFB = Input.GetAxis ("MoveVertical") * speed;
		moveLR = Input.GetAxis ("MoveHorizontal") * speed;

		rotX = Input.GetAxis ("MouseAxis1") * sensitivity;
		rotY -= Input.GetAxis ("MouseAxis2") * sensitivity;

		rotY = Mathf.Clamp (rotY, -60f, 60f);

		Vector3 movement = new Vector3 (moveLR, 0, moveFB);
		transform.Rotate (0, rotX, 0);
		eyes.transform.localRotation = Quaternion.Euler(rotY, 0, 0);
		//eyes.transform.Rotate (-rotY, 0, 0);

		movement = transform.rotation * movement;
        movement.y -= 4000f * Time.deltaTime;
        player.Move (movement * Time.deltaTime);

	}
	}
}