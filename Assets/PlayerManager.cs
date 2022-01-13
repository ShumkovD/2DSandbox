using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    //キャラクタの速度
    public float characterSpeed;
    //キャラクタに対しての重力加速度
    public float g;
    // Start is called before the first frame update
    Camera cam;
    Rigidbody2D rb;
    void Start()
    {
        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        cam.transform.position = new Vector3(transform.position.x, transform.position.y, -10);
    }

    float movementX;
    // Update is called once per frame
    void Update()
    {
        cam.transform.position = new Vector3(transform.position.x, transform.position.y, -10);
        movementX = Input.GetAxis("Horizontal") * Time.deltaTime * characterSpeed;
    }
    private void FixedUpdate()
    {
        Movement();
    }
    void Movement()
    {
        Vector3 movement = new Vector3(transform.position.x + movementX, transform.position.y, transform.position.z);
        transform.position = movement;
        if(Input.GetKeyDown(KeyCode.Space))
            rb.AddForce(Vector3.up*10.0f, ForceMode2D.Impulse);
    }

}
