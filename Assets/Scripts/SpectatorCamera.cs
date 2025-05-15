using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class SpectatorCamera : MonoBehaviour
{
    public Player target;

    public float xSpeed = 250.0f;
    public float ySpeed = 120.0f;
    public float distance = 10;

    public Vector2 DistanceRange;

    public float yMinLimit = -20;
    public float yMaxLimit = 80;

    private float x = 0.0f;
    private float y = 0.0f;

    private int playerindex = 0;
    private bool dead;
    private Vector3 trackingpos;
    private static SpectatorCamera instance;

    public static Player spectatingTarget => instance.target;

    void Start()
    {
        instance = this;
        var angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;
    }

    float prevDistance;

    void LateUpdate()
    {
        if(!NetworkManager.Singleton) return;  
        if (NetworkManager.Singleton.IsClient && GameManager.IsSpectating)
        {
            if (!dead) { target = null; trackingpos = transform.position; dead = true; }
            distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * 2, DistanceRange.x, DistanceRange.y);

            if (Player.PLAYERBYID.Values.Count > 0)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    playerindex--;
                    if (playerindex < 0) { playerindex = Player.PLAYERS.Count - 1; }
                    target = Player.PLAYERS[playerindex];
                }
                if (Input.GetMouseButtonDown(1))
                {
                    playerindex++;
                    if (playerindex >= Player.PLAYERS.Count) { playerindex = 0; }
                    target = Player.PLAYERS[playerindex];
                }
            }

            var pos = Input.mousePosition;
            var dpiScale = 1f;
            if (Screen.dpi < 1) dpiScale = 1;
            if (Screen.dpi < 200) dpiScale = 1;
            else dpiScale = Screen.dpi / 200f;

            if (pos.x < 380 * dpiScale && Screen.height - pos.y < 250 * dpiScale) return;

            if (!UIManager.GetPauseMenuOpen)
            {
                x += Input.GetAxis("Mouse X") * xSpeed * PlayerPrefs.GetFloat("SENS", 2) * 0.015f;
                y -= Input.GetAxis("Mouse Y") * ySpeed * PlayerPrefs.GetFloat("SENS", 2) * 0.015f;
            }

            y = ClampAngle(y, yMinLimit, yMaxLimit);
            var rotation = Quaternion.Euler(y, x, 0);
            var position = rotation * new Vector3(0.0f, 0.0f, -distance) + trackingpos;
            transform.SetPositionAndRotation(position, rotation);

            if (target)
            {
                trackingpos = target.GetPlayerCentre;
            }

            if (Mathf.Abs(prevDistance - distance) > 0.001f)
            {
                prevDistance = distance;
                var rot = Quaternion.Euler(y, x, 0);
                var po = rot * new Vector3(0.0f, 0.0f, -distance) + trackingpos;
                transform.rotation = rot;
                transform.position = po;
            }
        }
        else
        {
            if (dead) { dead = false; target = null; }
        }
    }

    static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360)
            angle += 360;
        if (angle > 360)
            angle -= 360;
        return Mathf.Clamp(angle, min, max);
    }
}
