using UnityEngine;
using UnityEngine.Networking;

public class MP_PlayerController : NetworkBehaviour
{


    public GameObject hud;

    private GameObject pointer; //pointer of player, origin of projectile
    private GameObject reticle;

    private Camera playerCam;
    private GameObject pauseMenu;
    private GameObject gvrController;
    private GameObject shield;

    /// Teleport Distance
    [Range(0.4f, 10.0f)]
    public float teleportDistance = 5.0f;
    private float movementSpeed = 400f;

    public MP_Player player;
    private MP_Wand playerWand;

    private Vector2 prevTouchPos;
    private Vector2 touchPos;
    private bool teleporting;
    private Vector3 newPos;

    [SyncVar(hook="OnHealthChanged")]
	public float health = 1.0f;
	[SyncVar(hook="OnManaChanged")]
	float mana = 1.0f;
	[SyncVar(hook="OnSpellIndexChanged")]
	int spellIndex = 0;

    [SyncVar(hook ="OnShieldToggle")]
    bool isShieldActive = false;

    const float MANA_REGEN_TIME = 0.1f;


    // Use this for initialization
    void Start()
    {
        string[] spellArray = { "Teleport", "MP_Fireball", "Network_Ball", "MP_Shield" };

        // Only the local player gets the GVRViewer camera
        if (isLocalPlayer)
        {
            SetupCamera();

            SetupPlayerModel();
        }
		else
		{
			//this.tag = "nonLocalPlayer";
		}
        this.tag = "Player";    


        // hook up the GvrController to this player when it is created
        // Note: this must be called before the wand and player can be instantiated
        SetupController();
        SetupWand();
        SetupShield();

        //begin player setup
        playerWand = new MP_Wand(pointer, reticle, 1, 1, spellArray);
        player = new MP_Player(gameObject, playerWand, teleportDistance);

        InvokeRepeating("CmdManaRegen", 0, MANA_REGEN_TIME);
    }


    // Update is called once per frame
    void Update()
    {
		//Debug.Log ("Current Player Health: " + player.getHealth ());
        // prevent non-local players from responding to input from the local system
        if (!isLocalPlayer)
        {
            return;
        }

        // Spell changing using gesture recognition
        if (GvrController.TouchDown)
        {
            prevTouchPos = GvrController.TouchPos;
        }

        if (GvrController.TouchUp)
        {
            touchPos = GvrController.TouchPos;
            int index = whichSwitch(prevTouchPos, touchPos);
			CmdSwitchSpell (index);
        }

        if (GvrController.AppButtonDown)
        {
            pauseMenu.SetActive(!pauseMenu.activeSelf);
        }
        else if (player.getSpellIndex() == 3
            && !pauseMenu.activeSelf)
        {
            Shield();
        }
        else if ((GvrController.ClickButtonDown || Input.GetMouseButtonDown(0))
            && !pauseMenu.activeSelf)
        {
            if (player.getSpellIndex() == 0)
            {
                SetTeleport();
            }
            else
            {
                CmdShoot();
            }
        }


        if (teleporting)
        {
            float step = movementSpeed * Time.deltaTime;
            this.transform.position = Vector3.MoveTowards(this.transform.position, newPos, step);
            if (this.transform.position == newPos)
            {
                teleporting = false;
            }
        }
        /*
        //Constant mana regeneration
        if (player.getSpellIndex() != 3 || (player.getSpellIndex() == 3 && !GvrController.ClickButton))
        {
            CmdManaRegen();
        }
        */
    }

    // Hook up the GvrViewer to this player
    void SetupCamera()
    {
        playerCam = Camera.main;

        playerCam.transform.position = transform.position + new Vector3(0,1,0);
        playerCam.transform.rotation = transform.rotation;
        playerCam.transform.SetParent(transform);

        // make the player unable to see their own model
        transform.GetChild(0).gameObject.layer = 8;
        transform.GetChild(0).GetChild(0).gameObject.layer = 8;
        transform.GetChild(0).GetChild(1).gameObject.layer = 8;

        pauseMenu = playerCam.transform.Find("PauseMenuCanvas/PauseMenu").gameObject;
        pauseMenu.SetActive(false);
    }

    // Hook up the GvrController to this player
    void SetupController()
    {
        gvrController = transform.Find("GvrControllerPointer").gameObject;

        if (gvrController != null)
        {
            Debug.Log("Found GvrControllerPointer");

            gvrController.transform.position = transform.position + new Vector3(0, 1, 0);
            gvrController.transform.rotation = transform.rotation;

            pointer = gvrController.transform.Find("Laser").gameObject;
            if (pointer != null)
            {
                Debug.Log("Found Laser");
                reticle = pointer.transform.Find("Reticle").gameObject;
                if (pointer != null) Debug.Log("Found Reticle\nFound all Controller objects");
            }
        }
    }

    void SetupWand()
    {
        GameObject controller = transform.Find("GvrControllerPointer/Controller").gameObject;
        Quaternion wandInitRot = controller.transform.rotation;

        GameObject newWand = GameObject.Instantiate(Resources.Load(PersistentData.data.myWand), controller.transform.position, wandInitRot) as GameObject;

        newWand.tag = "Old";

        newWand.transform.SetParent(controller.transform, false);

        newWand.transform.localPosition = new Vector3(0, 0, 0);
        newWand.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Disable Google's controller graphic
        GameObject controllerGraphic = controller.transform.Find("ddcontroller").gameObject;
        controllerGraphic.SetActive(false);
    }

	// Disable the Player Model to prevent issues with the pointer/teleport
	void SetupPlayerModel()
	{
		GameObject playerModel = transform.Find("PlayerModel").gameObject;
		//playerModel.SetActive(false);
	}

    void SetupShield()
    {
        shield = transform.Find("GvrControllerPointer/Controller/MP_Shield").gameObject;
        GameObject controller = transform.Find("GvrControllerPointer/Controller").gameObject;

        shield.transform.parent = controller.transform;
        shield.transform.localPosition = new Vector3(shield.transform.localPosition.x - 0.15f,
            shield.transform.localPosition.y + 0.4f, shield.transform.localPosition.z + 0.05f);

        shield.SetActive(false);
    }

    [Command]
    void CmdManaRegen()
    {
        mana += player.manaRegenSpeed;
        player.setMana(true, player.manaRegenSpeed);
    }

    // Shoot command called by the Client but run on the server
    // Command methods must be called by a class extending NetworkBehaviour and must be prefixed with "Cmd"
    [Command]
    void CmdShoot()
    {
		float mc = playerWand.getSpellCost();
        Debug.Log("Mana Cost: " + mc);
		if (player.getMana() >= mc)
		{
			string spell = playerWand.spells [playerWand.primarySpell];
			Debug.Log ("Shooting " + spell);
			var projectileClone = GameObject.Instantiate(Resources.Load(spell), pointer.transform.position + pointer.transform.forward * 0.3f, pointer.transform.rotation) as GameObject;

			if (projectileClone.GetComponent<Rigidbody>())
				projectileClone.GetComponent<Rigidbody>().AddForce(pointer.transform.forward * projectileClone.GetComponent<MP_Projectile>().getSpeed() * playerWand.speedMultiplier);

			NetworkServer.Spawn(projectileClone);

			mana -= mc;
			player.setMana(false, mc);
		}
    }

    // Conditions for enabling or disabling the player's shield
    void Shield()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        if (GvrController.ClickButtonDown && player.getMana() > .3f)
        {
            InvokeRepeating("CmdShieldManaDepletion", 0, MANA_REGEN_TIME);
            CmdActivateShield();
        }
        if (GvrController.ClickButton)
        {
            player.setMana(false, player.manaDepletionShield * Time.deltaTime);
            if (player.getMana() < 0.3f)
            {
                CancelInvoke("CmdShieldManaDepletion");
                CmdDisableShield();
            }
        }
        if (GvrController.ClickButtonUp)
        {
            CancelInvoke("CmdShieldManaDepletion");
            CmdDisableShield();
        }
    }

    [Command]
    void CmdShieldManaDepletion()
    {
        mana -= player.manaDepletionShield;
        player.setMana(false, player.manaDepletionShield);
    }

    [Command]
    void CmdActivateShield()
    {
        isShieldActive = true;
        shield.SetActive(true);
    }

    [Command]
    void CmdDisableShield()
    {
        isShieldActive = false;
        shield.SetActive(false);
    }

	/*
	 * Command for switch spell to update the client's spell index across the network
	 */
	[Command]
	void CmdSwitchSpell(int index)
	{
		spellIndex = index;
		player.switchSpell (index);
	}

	void OnHealthChanged(float newHealth)
	{
		health = newHealth;
		player.setHealth (health);
	}

	void OnManaChanged(float newMana)
	{
		mana = newMana;
		player.setMana(mana);
	}

	void OnSpellIndexChanged(int newSpellIndex)
	{
		spellIndex = newSpellIndex;
		playerWand.primarySpell = spellIndex;
	}

    void OnShieldToggle(bool newShieldState)
    {
        isShieldActive = newShieldState;
        shield.SetActive(isShieldActive);
    }

    int whichSwitch(Vector2 prev, Vector2 final)
    {
        float threshold = 0.3F;
        int index = player.getSpellIndex();

        float distx = final.x - prev.x;
        float disty = final.y - prev.y;

        // horizontal vs vertical priority
        if (Mathf.Abs(distx) > Mathf.Abs(disty))
        {
            // swipe right
            if (distx > threshold)
            {
                index = 1;

            }

            // swipe left
            else if (distx < -threshold)
            {
                index = 3;
            }
        }

        else
        {
            // swipe up
            if (disty > threshold)
            {
                index = 0;
            }

            // swipe down
            else if (disty < -threshold)
            {
                index = 2;
            }
        }

        // set current spell
        //Debug.Log("Distx: " + distx + " Disty: " + disty);

        return index;
    }

    public void teleport(bool needToTeleport, Vector3 _newPos)
    {
        teleporting = needToTeleport;
        newPos = _newPos;
        player.setMana(false, .2f);
    }

    void SetTeleport()
    {
        Ray ray = new Ray(pointer.transform.position, pointer.transform.rotation * Vector3.forward);
        RaycastHit raycastHit;

        if (Physics.Raycast(ray, out raycastHit))
        {
            GameObject target = raycastHit.collider.gameObject;
            Debug.Log("Raycast hit " + target.tag + " object");
            if (target.tag == "Teleport")
            {
                mana -= 0.2f;
                teleport(true, new Vector3(target.transform.position.x, transform.position.y, target.transform.position.z));
            }
        }
    }
}
