using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Parrot : MonoBehaviour
{
    private Animator parrot;
    // Start is called before the first frame update
    void Start()
    {
        parrot = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (parrot.GetCurrentAnimatorStateInfo(0).IsName("idle"))
        {
            parrot.SetBool("takeoff", false);
            parrot.SetBool("landing", false);
            parrot.SetBool("fly", false);
        }
        if (Input.GetKeyDown(KeyCode.W))
        {
            parrot.SetBool("idle", false);
            parrot.SetBool("walk", true);
        }
        if ((Input.GetKeyUp(KeyCode.W))||(Input.GetKeyUp(KeyCode.F))||(Input.GetKeyUp(KeyCode.R))||(Input.GetKeyUp(KeyCode.E))||(Input.GetKeyUp(KeyCode.A))||(Input.GetKeyUp(KeyCode.D)))
        {
            parrot.SetBool("walk", false);
            parrot.SetBool("idle", true);
            parrot.SetBool("dance", false);
            parrot.SetBool("glide", false);
            parrot.SetBool("fly", true);
            parrot.SetBool("eat", false);
            parrot.SetBool("idle", true);
            parrot.SetBool("jump", false);
            parrot.SetBool("flyleft", false);
            parrot.SetBool("flyright", false);
            parrot.SetBool("turnright", false);
            parrot.SetBool("turnleft", false);
        }
        if (Input.GetKeyDown(KeyCode.F))
        {
            parrot.SetBool("dance", true);
            parrot.SetBool("idle", false);
            parrot.SetBool("glide", true);
            parrot.SetBool("fly", false);
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            parrot.SetBool("takeoff", true);
            parrot.SetBool("idle", false);
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            parrot.SetBool("fly", false);
            parrot.SetBool("landing", true);
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            parrot.SetBool("eat", true);
            parrot.SetBool("idle", false);
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            parrot.SetBool("idle", false);
            parrot.SetBool("jump", true);
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            parrot.SetBool("fly", false);
            parrot.SetBool("flyleft", true);
            parrot.SetBool("turnleft", true);
            parrot.SetBool("idle", false);
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            parrot.SetBool("fly", false);
            parrot.SetBool("flyright", true);
            parrot.SetBool("turnright", true);
            parrot.SetBool("idle", false);
        }
        if (Input.GetKeyDown(KeyCode.Keypad0))
        {
            parrot.SetBool("idle", false);
            parrot.SetBool("die", true);
        }
    }
}
