﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class InputManager : MonoBehaviour
{
	public static InputManager Instance { get { return GetInstance(); } }

	#region Singleton

	private static InputManager instance;
	private static InputManager GetInstance()
	{
		if (instance == null)
		{
			instance = FindObjectOfType<InputManager>();
		}
		return instance;
	}
	#endregion

	public static Action<float> DashAttackEvent;

	public EnumStorage.PlayerState CurrentPlayerState = EnumStorage.PlayerState.Idle;



	#region Getter/Setter
	public float DashDistance { get { return dashDistance; } set { dashDistance = value; } }
	public float MoveSpeed { get { return moveSpeed; } set { moveSpeed = value; } }
	public float JumpHeight { get { return jumpHeight; } set { jumpHeight = value; } }

	public float CurrentPos { get { return movementCam.GetComponent<CinemachineVirtualCamera>().GetCinemachineComponent<CinemachineTrackedDolly>().m_PathPosition; } }
	public float CurrentDashDelay { get { return currentDashDelay; } }
	public float DashDelay { get { return dashDelay; } }
	#endregion

	[Header("Settings: ")]
	[SerializeField] private float moveSpeed = 5f;
	[SerializeField] private float jumpHeight = 2f;
	[SerializeField] private int maxJumpAmount = 1;
	[SerializeField] private float gravity = -9.81f;
	[SerializeField] private float groundDistance = 0.2f;
	[SerializeField] private LayerMask groundLayer;
	[SerializeField] private LayerMask attackMask;
	[SerializeField] private Vector3 drag;

	[Header("DashSettings: ")]
	[SerializeField] private float dashStutterTime = 0.05f;
	[SerializeField] private int dashDamage = 10;
	[SerializeField] private float dashDistance = 5f;
	[SerializeField] private float dashDelay = 1f;
	[SerializeField] private float dashDuration = 0.5f;
	[SerializeField] private AnimationCurve dashCurve;

	[Header("References: ")]
	[SerializeField] private GameObject rotationCam;
	[SerializeField] private GameObject movementCam;
	[SerializeField] private MovementTrack currentMovementTrack;

	private Animator anim;
	private int currentJumpAmount = 1;
	private float currentDashDelay;
	private Vector2 velocity;
	private bool isGrounded = true;
	private Transform groundChecker;
	private bool isTurned = false;
	private float horizontal = 0f;

	private void Start()
	{
		anim = GetComponentInChildren<Animator>();
		groundChecker = transform.GetChild(0);
		currentDashDelay = 0;
	}

	private void Update()
	{
		if (CurrentPlayerState != EnumStorage.PlayerState.Dead)
		{
			HandleMovement();
			HandleRotation();
		}
	}

	private void HandleRotation()
	{
		float _turnRot = isTurned ? -90 : 90;
		transform.eulerAngles = new Vector3(0, rotationCam.transform.eulerAngles.y + _turnRot, 0);
	}

	private void HandleMovement()
	{
		CinemachineTrackedDolly _dolly = movementCam.GetComponent<CinemachineVirtualCamera>().GetCinemachineComponent<CinemachineTrackedDolly>();
		float _pathLenght = _dolly.m_Path.PathLength;
		horizontal = Input.GetAxis("Horizontal");

		isGrounded = Physics.CheckSphere(groundChecker.position, groundDistance, groundLayer, QueryTriggerInteraction.Ignore);
		anim.SetBool("isGrounded", isGrounded);

		CoolDownDash();


		//Walk
		if (CurrentPlayerState != EnumStorage.PlayerState.Dashing)
		{
			if (horizontal != 0)
			{
				CurrentPlayerState = EnumStorage.PlayerState.Moving;
				isTurned = horizontal > 0 ? false : true;
				Walk();
			}
			else
			{
				CurrentPlayerState = EnumStorage.PlayerState.Idle;
				velocity.x = 0;
			}
		}

		//Jump With DoubleJump
		if (Input.GetKeyDown(KeyCode.Space) && currentJumpAmount > 0)
			Jump();

		//Dash
		if (Input.GetKeyDown(KeyCode.LeftShift) && currentDashDelay <= 0)
			Dash();

		//Apply velocity
		velocity.y += gravity * Time.deltaTime;

		velocity.y /= 1 + drag.y * Time.deltaTime;
		CheckIfGrounded();

		if (CurrentPlayerState != EnumStorage.PlayerState.Dashing)
			TransformPosition(_dolly.m_PathPosition + velocity.x);

		transform.position = new Vector3(movementCam.transform.position.x, transform.position.y + velocity.y, movementCam.transform.position.z);// * Time.deltaTime;

		//_dolly.m_PathPosition = Mathf.Clamp(_dolly.m_PathPosition + velocity.x, 0, _pathLenght);
		//_dolly.m_PathPosition = BoundaryManager.Instance.ClampDistance(_dolly.m_PathPosition);
		//transform.position = new Vector3(movementCam.transform.position.x, transform.position.y + velocity.y, movementCam.transform.position.z);// * Time.deltaTime;

		//BoundaryManager.Instance.CheckIfWithinBoundary(_dolly.m_PathPosition);
	}

	private void TransformPosition(float _newPos)
	{
		CinemachineTrackedDolly _dolly = movementCam.GetComponent<CinemachineVirtualCamera>().GetCinemachineComponent<CinemachineTrackedDolly>();
		float _pathLenght = _dolly.m_Path.PathLength;

		_dolly.m_PathPosition = Mathf.Clamp(_newPos, 0, _pathLenght);
		_dolly.m_PathPosition = BoundaryManager.Instance.ClampDistance(_dolly.m_PathPosition);

		BoundaryManager.Instance.CheckIfWithinBoundary(_dolly.m_PathPosition);
	}

	/// <summary>
	/// Moves the player across the dolly track according to the given horizontal input
	/// </summary>
	private void Walk()
	{
		//CinemachineTrackedDolly _dolly = movementCam.GetComponent<CinemachineVirtualCamera>().GetCinemachineComponent<CinemachineTrackedDolly>();
		//float _pathLenght = _dolly.m_Path.PathLength;
		//float _camPos = _dolly.m_PathPosition;

		velocity.x = (isTurned ? -Time.deltaTime : Time.deltaTime) * moveSpeed;
		anim.SetFloat("Movement", horizontal);

		//anim.SetBool("isRunning", true);
		//_dolly.m_PathPosition = _camPos;

		//transform.position = new Vector3(movementCam.transform.position.x, transform.position.y, movementCam.transform.position.z);
	}

	/// <summary>
	/// Launches the player upwards according to the amount of jumps available.
	/// </summary>
	private void Jump()
	{
		if (currentJumpAmount == maxJumpAmount)
			anim.SetTrigger("Jump");

		velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
		isGrounded = false;
		currentJumpAmount--;
	}

	/// <summary>
	/// Launches the player sideways towards the current direction.
	/// </summary>
	private void Dash()
	{
		CurrentPlayerState = EnumStorage.PlayerState.Dashing;
		anim.SetBool("isDashing", true);
		if (dashRoutine != null) StopCoroutine(dashRoutine);
		dashRoutine = StartCoroutine(IDash());
	}

	private Coroutine dashRoutine;
	private IEnumerator IDash()
	{
		CinemachineTrackedDolly _dolly = movementCam.GetComponent<CinemachineVirtualCamera>().GetCinemachineComponent<CinemachineTrackedDolly>();
		float _startPos = _dolly.m_PathPosition;
		float _pathLenght = _dolly.m_Path.PathLength;

		float _lerpTime = 0;

		List<EnemyParent> _enemyHitList = new List<EnemyParent>();

		while (_lerpTime < 1)
		{
			Debug.Log(dashDuration);
			_lerpTime += Time.deltaTime / dashDuration;
			float _lerpKey = dashCurve.Evaluate(_lerpTime);

			float _newPos = Mathf.Clamp(Mathf.Lerp(_startPos, _startPos + (isTurned ? -dashDistance : dashDistance), _lerpKey), 0, _pathLenght);
			TransformPosition(_newPos);

			//_dolly.m_PathPosition = Mathf.Clamp(Mathf.Lerp(_startPos, _startPos + (isTurned ? -dashDistance : dashDistance), _lerpKey), 0, _pathLenght);
			//_dolly.m_PathPosition = BoundaryManager.Instance.ClampDistance(_dolly.m_PathPosition);

			yield return null;

			RaycastHit[] _hits = Physics.SphereCastAll(GetComponentInChildren<Renderer>().bounds.center + transform.forward / 2, 1, transform.forward, 0, attackMask);

			if (_hits != null)
			{
				foreach (var _hit in _hits)
				{
					Debug.Log(_hit.transform.gameObject.name);
					EnemyParent _enemy = _hit.transform.GetComponentInParent<EnemyParent>();

					if (_enemy != null && !_enemyHitList.Contains(_enemy))
					{
						_enemyHitList.Add(_enemy);
						DashAttackEvent += _enemy.TakeDamage;
						yield return new WaitForSeconds(dashStutterTime);
					}
				}
			}

			yield return null;
		}

		anim.SetBool("isDashing", false);


		//velocity.x = 0;

		//float _value = dashDistance * Mathf.Log(1f / (Time.deltaTime * drag.x + 1)) / -Time.deltaTime;
		//velocity.x += isTurned ? -_value : _value; // Vector3.Scale(transform.forward, dashDistance * new Vector3((), 0, 0));
		currentDashDelay = dashDelay;

		CurrentPlayerState = EnumStorage.PlayerState.Idle;

		//anim.SetBool("isDashing", true);

		if (DashAttackEvent != null)
			DashAttackEvent(dashDamage);

		yield return null;
	}

	private void CoolDownDash()
	{
		if (currentDashDelay > 0)
		{
			if (currentDashDelay > dashDelay - 1)
			{
				velocity.x = 0;
			}
			currentDashDelay -= Time.deltaTime;
		}

		else
			currentDashDelay = 0;
	}

	/// <summary>
	/// Checks if the player is currently touching the ground (not in the air).
	/// </summary>
	private void CheckIfGrounded()
	{
		if (isGrounded && velocity.y < 0)
		{
			velocity.y = 0f;
			currentJumpAmount = maxJumpAmount;
		}
	}

	private void OnDrawGizmos()
	{
		Gizmos.DrawSphere(GetComponentInChildren<Renderer>().bounds.center + transform.forward / 2, 1);
	}
}