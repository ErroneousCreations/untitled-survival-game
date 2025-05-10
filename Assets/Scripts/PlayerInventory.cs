using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.Netcode;
using DG.Tweening;
using Unity.Collections;

public class PlayerInventory : NetworkBehaviour
{
    public NetworkVariable<bool> backpackOpen = new NetworkVariable<bool>(false, writePerm: NetworkVariableWritePermission.Owner);

    // Hands
    public NetworkVariable<ItemData> leftHand = new NetworkVariable<ItemData>(default, writePerm: NetworkVariableWritePermission.Owner);
    public NetworkVariable<ItemData> rightHand = new NetworkVariable<ItemData>(default, writePerm: NetworkVariableWritePermission.Owner);

    // Hotbar
    public List<ItemData> hotbar = new();

    // Backpack (stack-based)
    private List<ItemData> backpack = new();
    [SerializeField] private float backpackCapacity = 30; //in litres

    [SerializeField] private int maxHotbarSize = 4;
    [SerializeField] private int currEquippedSlot = 0;

    [Header("Visuals")]
    [SerializeField] private Transform LeftHandOb;
    [SerializeField] private Transform RightHandOb;
    [SerializeField] private MeshRenderer LefthandItemRend, RighthandItemRend;
    [SerializeField] private MeshFilter LefthandItemMesh, RIghthandItemMesh;
    private Vector3 initialLeftHandPos, initialRightHandPos;

    //syncing visuals
    private NetworkVariable<Vector3> currLefthandParentPos = new(default, writePerm: NetworkVariableWritePermission.Owner), currRighthandParentPos = new(default, writePerm: NetworkVariableWritePermission.Owner), currLefthandPos = new(default, writePerm: NetworkVariableWritePermission.Owner), currRighthandPos = new(default, writePerm: NetworkVariableWritePermission.Owner), currLeftHandRot = new(default, writePerm: NetworkVariableWritePermission.Owner), currRightHandRot = new(default, writePerm: NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> currLefthandVisible = new(default, writePerm: NetworkVariableWritePermission.Owner), currRighthandVisible = new(default, writePerm: NetworkVariableWritePermission.Owner), currLefthandItemVisible = new(default, writePerm: NetworkVariableWritePermission.Owner), currRighthandItemVisible = new(default, writePerm: NetworkVariableWritePermission.Owner);
    private NetworkVariable<FixedString4096Bytes> savedInventoryData = new(default, writePerm: NetworkVariableWritePermission.Owner);

    private bool isCrafting = false;
    private bool busy = false;
    private float craftingTime = 0;
    private float itemBusyTime = 0;
    private static PlayerInventory localInstance;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner) { localInstance = this; Player.LocalPlayer.OnDied += Died; }
        UpdateLefthandVisuals();
        UpdateRighthandVisuals();
    }

    public string GetSavedData => savedInventoryData.Value.ToString();

    public static void AddItemBusyTime(float amount) { localInstance.itemBusyTime = amount; }
    public static bool GetIsBusy => localInstance.itemBusyTime > 0 || localInstance.busy || localInstance.isCrafting;

    public bool GetBusy => itemBusyTime > 0 || busy || isCrafting;

    public static ItemData GetLeftHandItem => localInstance.leftHand.Value;
    public static ItemData GetRightHandItem => localInstance.rightHand.Value;

    public static void UpdateRightHandSaveData(List<FixedString128Bytes> data)
    {
        ItemData righthand = localInstance.rightHand.Value;
        righthand.SavedData = data;
        localInstance.rightHand.Value = righthand;
        localInstance.hotbar[localInstance.currEquippedSlot] = localInstance.rightHand.Value; // Update hotbar with new item
        localInstance.UpdateRighthandVisuals();
    }

    public static void UpdateLeftHandSaveData(List<FixedString128Bytes> data)
    {
        ItemData lefthand = localInstance.leftHand.Value;
        lefthand.SavedData = data;
        localInstance.leftHand.Value = lefthand;
        localInstance.UpdateLefthandVisuals();
    }

    public static void UpdateRightHandItemData(ItemData data)
    {
        localInstance.rightHand.Value = data;
        localInstance.hotbar[localInstance.currEquippedSlot] = localInstance.rightHand.Value; // Update hotbar with new item
        localInstance.UpdateRighthandVisuals();
    }

    public static void UpdateLeftHandItemData(ItemData data)
    {
        localInstance.leftHand.Value = data;
        localInstance.UpdateLefthandVisuals();
    }

    public static List<FixedString128Bytes> GetLeftHandSaveData => localInstance.leftHand.Value.SavedData;
    public static List<FixedString128Bytes> GetRightHandSaveData => localInstance.rightHand.Value.SavedData;

    #region Stuff for the items to use since they cant have their own variables and be networked etc

    public static void SpawnItemWithVelocity(ItemData item, Vector3 position, Vector3 velocity, ulong thrower, Vector3 angular = default)
    {
        localInstance.SpawnItemVelocityWorldRPC(item, position, velocity, thrower, angular);
    }

    [Rpc(SendTo.Server)]
    private void SpawnItemVelocityWorldRPC(ItemData item, Vector3 position, Vector3 velocity, ulong thrower, Vector3 angular)
    {
        // Spawn logic should be synced
        var go = Instantiate(ItemDatabase.GetItem(item.ID.ToString()).ItemPrefab, position, Quaternion.identity);
        go.NetworkObject.Spawn();
        go.transform.up = velocity.normalized;
        go.rb.linearVelocity = velocity;
        go.rb.angularVelocity = angular;
        go.InitThrown(thrower);
        go.CurrentSavedData.Clear();
        foreach (var data in item.SavedData)
        {
            go.CurrentSavedData.Add(data.ToString());
        }
    }

    public static void DeleteLefthandItem()
    {
        localInstance.leftHand.Value = ItemData.Empty;
        localInstance.UpdateLefthandVisuals();
    }

    public static void DeleteRighthandItem()
    {
        localInstance.rightHand.Value = ItemData.Empty;
        localInstance.hotbar[localInstance.currEquippedSlot] = ItemData.Empty;
        localInstance.UpdateRighthandVisuals();
    }

    #endregion


    private void Start()
    {
        if (!IsOwner) return;
        // Fill hotbar with null if needed
        for (int i = 0; i < maxHotbarSize; i++) hotbar.Add(ItemData.Empty);

        // Set initial positions
        initialLeftHandPos = LeftHandOb.localPosition;
        initialRightHandPos = RightHandOb.localPosition;
    }

    private void Update()
    {
        if (!IsOwner) { LocalSync(); return; }
        UpdateSaveableData();
        UpdateItems();
        HandleInput();
        HandleSyncing();
    }

    public void InitFromSavedData(string data)
    {
        if (data == "null") { return; }
        var split = data.Split('|');
        leftHand.Value = new ItemData(split[0]);
        currEquippedSlot = int.Parse(split[1]);
        for (int i = 0; i < maxHotbarSize; i++)
        {
            hotbar[i] = new ItemData(split[i + 2]);
        }
        for (int i = maxHotbarSize; i < split.Length; i++)
        {
            backpack.Add(new ItemData(split[i]));
        }
        rightHand.Value = hotbar[currEquippedSlot];

        if (rightHand.Value.IsValid && ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour != null)
        {
            rightHand.Value = ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour.OnLoaded(rightHand.Value); //call hand update
            hotbar[currEquippedSlot] = rightHand.Value;
        }

        if (leftHand.Value.IsValid && ItemDatabase.GetItem(leftHand.Value.ID.ToString()).ItemBehaviour != null)
        {
            leftHand.Value = ItemDatabase.GetItem(leftHand.Value.ID.ToString()).ItemBehaviour.OnLoaded(leftHand.Value); //call hand update
        }

        for (int i = 0; i < backpack.Count; i++)
        {
            var item = backpack[i];
            if (item.IsValid && ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour != null)
            {
                item = ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour.OnLoaded(item); //call backpack update
            }
            backpack[i] = item;
        }

        for (int i = 0; i < hotbar.Count; i++)
        {
            if (i == currEquippedSlot) { continue; }
            var item = hotbar[i];
            if (item.IsValid && ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour != null)
            {
                item = ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour.OnLoaded(item); //call hotbar update
            }
            hotbar[i] = item;
        }

        UpdateRighthandVisuals();
        UpdateLefthandVisuals();
    }

    private void UpdateSaveableData()
    {
        savedInventoryData.Value = "";
        savedInventoryData.Value += leftHand.Value.ToString() + "|";
        savedInventoryData.Value += currEquippedSlot.ToString() + "|";
        foreach (var item in hotbar)
        {
            savedInventoryData.Value += item.ToString() + "|";
        }
        foreach (var item in backpack)
        {
            savedInventoryData.Value += item.ToString() + "|";
        }
        savedInventoryData.Value = savedInventoryData.Value.ToString()[..^1];
    }

    private void UpdateItems()
    {
        if (rightHand.Value.IsValid && ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour != null)
        {
            rightHand.Value = ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour.OnHeldUpdate(RightHandOb.GetChild(0), HandSideEnum.Right, rightHand.Value); //call hand update
            hotbar[currEquippedSlot] = rightHand.Value;
        }

        if (leftHand.Value.IsValid && ItemDatabase.GetItem(leftHand.Value.ID.ToString()).ItemBehaviour != null)
        {
            leftHand.Value = ItemDatabase.GetItem(leftHand.Value.ID.ToString()).ItemBehaviour.OnHeldUpdate(LeftHandOb.GetChild(0), HandSideEnum.Left, leftHand.Value); //call hand update
        }

        for (int i = 0; i < backpack.Count; i++)
        {
            var item  = backpack[i];
            if (item.IsValid && ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour != null)
            {
                item = ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour.InInventoryUpdate(InventoryItemStatus.InBackpack, item); //call backpack update
            }
            backpack[i] = item;
        }

        for (int i = 0; i < hotbar.Count; i++)
        {
            if (i == currEquippedSlot) { continue; }
            var item = hotbar[i];
            if (item.IsValid && ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour != null)
            {
                item = ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour.InInventoryUpdate(InventoryItemStatus.InHotbar, item); //call hotbar update
            }
            hotbar[i] = item;
        }
    }

    private void LocalSync()
    {
        LeftHandOb.localPosition = currLefthandParentPos.Value;
        RightHandOb.localPosition = currRighthandParentPos.Value;
        LeftHandOb.GetChild(0).localPosition = currLefthandPos.Value;
        RightHandOb.GetChild(0).localPosition=currRighthandPos.Value;
        LeftHandOb.GetChild(0).localRotation = Quaternion.Euler(currLeftHandRot.Value);
        RightHandOb.GetChild(0).localRotation = Quaternion.Euler(currRightHandRot.Value);
        LeftHandOb.gameObject.SetActive(currLefthandVisible.Value);
        RightHandOb.gameObject.SetActive(currRighthandVisible.Value);
        LefthandItemMesh.gameObject.SetActive(currLefthandItemVisible.Value);
        RIghthandItemMesh.gameObject.SetActive(currRighthandItemVisible.Value);

    }

    private void HandleSyncing()
    {
        currLefthandParentPos.Value = LeftHandOb.localPosition;
        currRighthandParentPos.Value = RightHandOb.localPosition;
        currLefthandPos.Value = LeftHandOb.GetChild(0).localPosition;
        currRighthandPos.Value = RightHandOb.GetChild(0).localPosition;
        currLeftHandRot.Value = LeftHandOb.GetChild(0).localRotation.eulerAngles;
        currRightHandRot.Value = RightHandOb.GetChild(0).localRotation.eulerAngles;
        currLefthandVisible.Value = LeftHandOb.gameObject.activeSelf;
        currRighthandVisible.Value = RightHandOb.gameObject.activeSelf;
        currLefthandItemVisible.Value = LefthandItemMesh.gameObject.activeSelf;
        currRighthandItemVisible.Value = RIghthandItemMesh.gameObject.activeSelf;
    }

    private float GetBackpackFillAmount
    {
        get
        {
            float totalVolume = 0;
            foreach (var item in backpack)
            {
                totalVolume += GetItemVolume(item);
            }
            return totalVolume;
        }
    }

    private float GetItemVolume(ItemData item)
    {
        return ItemDatabase.GetItem(item.ID.ToString()).Volume;
    }

    public void PickupItemR(ItemData item)
    {
        // If backpack is open, add to backpack
        if (backpackOpen.Value && GetBackpackFillAmount+GetItemVolume(item) <= backpackCapacity)
        {
            backpack.Add(item);
            return;
        }
        if(!rightHand.Value.IsValid)
        {
           StartCoroutine(RightHandPickup(true, item));
        }
        else
        {
            StartCoroutine(RightHandPickup(false, item));
        }
    }

    private IEnumerator RightHandPickup(bool startempty, ItemData item)
    {
        if (startempty)
        {
            busy = true;
            RightHandOb.localPosition = new Vector3(initialRightHandPos.x, -0.5f, initialRightHandPos.z);
            RightHandOb.DOLocalMove(initialRightHandPos, 0.3f);
            if (item.IsValid && ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour != null)
            {
                item = ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour.OnPickup(item, HandSideEnum.Right); //call onpickup
            }
            rightHand.Value = item;
            hotbar[currEquippedSlot] = item; // Update hotbar with new item
            UpdateRighthandVisuals();
            yield return new WaitForSeconds(0.3f);
            busy = false;
        }
        else
        {
            busy = true;
            RightHandOb.DOLocalMove(new Vector3(initialRightHandPos.x, -0.5f, initialRightHandPos.z), 0.3f);
            yield return new WaitForSeconds(0.3f);
            SpawnItemWorldRPC(rightHand.Value, Player.GetLocalPlayerCentre + transform.forward);
            if (item.IsValid && ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour != null)
            {
                item = ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour.OnPickup(item, HandSideEnum.Right); //call onpickup
            }
            rightHand.Value = item;
            hotbar[currEquippedSlot] = rightHand.Value; // Update hotbar with new item
            UpdateRighthandVisuals();
            RightHandOb.DOLocalMove(initialRightHandPos, 0.3f);
            yield return new WaitForSeconds(0.3f);
            busy = false;
        }
    }

    public void PickupItemL(ItemData item)
    {
        // If backpack is open, add to backpack
        if (backpackOpen.Value && GetBackpackFillAmount + GetItemVolume(item) <= backpackCapacity)
        {
            backpack.Add(item); //todo add backpack max capacity
            return;
        }
        if (!leftHand.Value.IsValid)
        {
            StartCoroutine(LeftHandPickup(true, item));
        }
        else
        {
            StartCoroutine(LeftHandPickup(false, item));
        }
    }

    private IEnumerator LeftHandPickup(bool startempty, ItemData item)
    {
        if (startempty)
        {
            busy = true;
            LeftHandOb.localPosition = new Vector3(initialLeftHandPos.x, -0.5f, initialLeftHandPos.z);
            LeftHandOb.DOLocalMove(initialLeftHandPos, 0.3f);
            if (item.IsValid && ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour != null)
            {
                item = ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour.OnPickup(item, HandSideEnum.Left); //call onpickup
            }
            leftHand.Value = item;
            UpdateLefthandVisuals();
            yield return new WaitForSeconds(0.3f);
            busy = false;
        }
        else
        {
            busy = true;
            LeftHandOb.DOLocalMove(new Vector3(initialLeftHandPos.x, -0.5f, initialLeftHandPos.z), 0.3f);
            yield return new WaitForSeconds(0.3f);
            SpawnItemWorldRPC(leftHand.Value, Player.GetLocalPlayerCentre + transform.forward);
            LeftHandOb.DOLocalMove(initialLeftHandPos, 0.3f);
            if (item.IsValid && ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour != null) {
                item = ItemDatabase.GetItem(item.ID.ToString()).ItemBehaviour.OnPickup(item, HandSideEnum.Left); //call onpickup
            }
            leftHand.Value = item;
            UpdateLefthandVisuals();
            yield return new WaitForSeconds(0.3f);
            busy = false;
        }
    }

    private void UpdateLefthandVisuals()
    {
        if (leftHand.Value.IsValid) {
            LefthandItemRend.materials = ItemDatabase.GetItem(leftHand.Value.ID.ToString()).HeldMats;
            LefthandItemMesh.mesh = ItemDatabase.GetItem(leftHand.Value.ID.ToString()).HeldMesh;
            LeftHandOb.gameObject.SetActive(true);
            if (LefthandItemMesh.transform.childCount > 0)
            {
                foreach (Transform child in RIghthandItemMesh.transform)
                {
                    Destroy(child.gameObject);
                }
            }
        }
        else
        {
            LeftHandOb.gameObject.SetActive(false);
        }
        UpdateLocalLefthandVisualsRPC(leftHand.Value);
    }

    [Rpc(SendTo.NotOwner)]
    private void UpdateLocalLefthandVisualsRPC(ItemData item)
    {
        if (item.IsValid)
        {
            LefthandItemRend.materials = ItemDatabase.GetItem(item.ID.ToString()).HeldMats;
            LefthandItemMesh.mesh = ItemDatabase.GetItem(item.ID.ToString()).HeldMesh;
        }
    }

    private void UpdateRighthandVisuals()
    {
        if (rightHand.Value.IsValid)
        {
            RighthandItemRend.materials = ItemDatabase.GetItem(rightHand.Value.ID.ToString()).HeldMats;
            RIghthandItemMesh.mesh = ItemDatabase.GetItem(rightHand.Value.ID.ToString()).HeldMesh;
            RightHandOb.gameObject.SetActive(true);
            if (RIghthandItemMesh.transform.childCount > 0) {
                foreach (Transform child in RIghthandItemMesh.transform)
                {
                    Destroy(child.gameObject);
                }
            }
        }
        else
        {
            RightHandOb.gameObject.SetActive(false);
            if (RIghthandItemMesh.transform.childCount > 0)
            {
                foreach (Transform child in RIghthandItemMesh.transform)
                {
                    Destroy(child.gameObject);
                }
            }
        }
        UpdateLocalRighthandVisualsRPC(rightHand.Value);
    }

    [Rpc(SendTo.NotOwner)]
    private void UpdateLocalRighthandVisualsRPC(ItemData item)
    {
        if (item.IsValid)
        {
            RighthandItemRend.materials = ItemDatabase.GetItem(item.ID.ToString()).HeldMats;
            RIghthandItemMesh.mesh = ItemDatabase.GetItem(item.ID.ToString()).HeldMesh;
        }
    }

    private void HandleInput()
    {
        if (itemBusyTime > 0) { itemBusyTime -= Time.deltaTime; }

        if(!busy && !isCrafting && itemBusyTime <= 0 && !Player.GetCanStand)
        {
            if (rightHand.Value.IsValid) {
                ForcedropRight();
                UpdateRighthandVisuals();
            }
            if (leftHand.Value.IsValid) {
                ForcedropLeft();
                UpdateLefthandVisuals();
            }
        }

        //if (Input.GetKeyDown(KeyCode.Tab) && !busy && !isCrafting && itemBusyTime<=0)
        //{
        //    backpackOpen.Value = !backpackOpen.Value;
        //}

        // Drop
        if (Input.GetKeyDown(KeyCode.Q) && !busy && !isCrafting && leftHand.Value.IsValid && itemBusyTime <= 0) StartCoroutine(DropLeftItem());
        if (Input.GetKeyDown(KeyCode.E) && !busy && !isCrafting && rightHand.Value.IsValid && itemBusyTime <= 0) StartCoroutine(DropRightItem());

        // Swap Hands
        if (Input.GetKeyDown(KeyCode.Z) && !busy && !isCrafting && itemBusyTime <= 0) StartCoroutine(SwapHandsCoroutine());

        // Put right hand into backpack
        if (Input.GetKeyDown(KeyCode.X) && !busy && !isCrafting && itemBusyTime <= 0)
        {
            if(rightHand.Value.IsValid ?
                GetBackpackFillAmount + GetItemVolume(rightHand.Value) <= backpackCapacity :
                backpack.Count > 0)
            {
                StartCoroutine(PutToBackpack());
            }
        }

        // Craft
        if (Input.GetKeyDown(KeyCode.C) && !busy && !isCrafting && leftHand.Value.IsValid && rightHand.Value.IsValid && itemBusyTime<=0)
        {
            isCrafting = true;
            craftingTime = 0;
        }

        if (isCrafting)
        {
            craftingTime += Time.deltaTime;
            RightHandOb.localPosition = Vector3.Lerp(initialRightHandPos, new Vector3(0.05f, initialRightHandPos.y, initialRightHandPos.z) + 0.01f * craftingTime * Random.insideUnitSphere, craftingTime/2);
            LeftHandOb.localPosition = Vector3.Lerp(initialLeftHandPos, new Vector3(-0.05f, initialLeftHandPos.y, initialLeftHandPos.z) + 0.01f * craftingTime * Random.insideUnitSphere, craftingTime/2);
            if (craftingTime >= 2)
            {
                isCrafting = false;
                StartCoroutine(ReturnHandsFromCrafting());
                TryCraft(leftHand.Value.ID.ToString(), rightHand.Value.ID.ToString());
                UpdateLefthandVisuals();
                UpdateRighthandVisuals();
            }
        }

        if(isCrafting && Input.GetKeyUp(KeyCode.C)) { isCrafting = false; craftingTime = 0; StartCoroutine(ReturnHandsFromCrafting()); } // cancel crafting if C is released

        if (busy ||isCrafting || itemBusyTime>0) return;

        // Hotbar swap (right hand)
        for (int i = 0; i < maxHotbarSize; i++)
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) //if theyre both invalid dont bother lmao
            {
                if(!(!rightHand.Value.IsValid && !hotbar[i].IsValid)) { currEquippedSlot = i; }
                else { StartCoroutine(SwapHotbarItem(i)); }
            }
        }
    }

    private IEnumerator ReturnHandsFromCrafting()
    {
        busy = true;
        RightHandOb.DOLocalMove(initialRightHandPos, 0.15f);
        LeftHandOb.DOLocalMove(initialLeftHandPos, 0.15f);
        yield return new WaitForSeconds(0.15f);
        busy = false;
    }

    private IEnumerator SwapHotbarItem(int i)
    {
        busy = true;
        RightHandOb.gameObject.SetActive(true);
        RIghthandItemMesh.gameObject.SetActive(rightHand.Value.IsValid);
        RightHandOb.DOLocalMove(new Vector3(initialRightHandPos.x, -0.5f, initialRightHandPos.z), 0.3f);
        yield return new WaitForSeconds(0.3f);
        if (rightHand.Value.IsValid && ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour != null) {
            rightHand.Value = ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour.OnUnequip(rightHand.Value);
            hotbar[currEquippedSlot] = rightHand.Value;
        }
        rightHand.Value = hotbar[i];
        if (rightHand.Value.IsValid && ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour != null)
        {
            rightHand.Value = ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour.OnEquip(rightHand.Value);
            hotbar[i] = rightHand.Value;
        }
        //update held graphics idk
        currEquippedSlot = i;
        UpdateRighthandVisuals();
        RightHandOb.DOLocalMove(initialRightHandPos, 0.3f);
        RIghthandItemMesh.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.3f);
        busy = false;
    }

    private IEnumerator PutToBackpack()
    {
        busy = true;
        RightHandOb.gameObject.SetActive(true);
        RIghthandItemMesh.gameObject.SetActive(rightHand.Value.IsValid);    
        RightHandOb.DOLocalMove(new Vector3(initialRightHandPos.x, initialRightHandPos.y, -0.25f), 0.3f);
        yield return new WaitForSeconds(0.3f);
        if (rightHand.Value.IsValid)
        {
            if (rightHand.Value.IsValid && ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour != null)
            {
                rightHand.Value = ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour.OnPutInBackpack(rightHand.Value);
            }
            backpack.Add(rightHand.Value);
            rightHand.Value = ItemData.Empty;
            hotbar[currEquippedSlot] = rightHand.Value; // Update hotbar with new item
            UpdateRighthandVisuals();
        }
        else
        {
            if (backpack.Count > 0)
            {
                rightHand.Value = backpack[^1];
                if (rightHand.Value.IsValid && ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour != null)
                {
                    rightHand.Value = ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour.OnRemoveFromBackpack(rightHand.Value);
                }
                backpack.RemoveAt(backpack.Count - 1);
                hotbar[currEquippedSlot] = rightHand.Value; // Update hotbar with new item
                UpdateRighthandVisuals();
            }
        }
        RIghthandItemMesh.gameObject.SetActive(true);
        RightHandOb.DOLocalMove(initialRightHandPos, 0.3f);
        yield return new WaitForSeconds(0.3f);
        busy = false;
    }

    private void ForcedropLeft()
    {
        if (leftHand.Value.IsValid && ItemDatabase.GetItem(leftHand.Value.ID.ToString()).ItemBehaviour != null)
        {
            leftHand.Value = ItemDatabase.GetItem(leftHand.Value.ID.ToString()).ItemBehaviour.OnDrop(leftHand.Value, HandSideEnum.Left);
        }
        SpawnItemWorldRPC(leftHand.Value, Player.GetLocalPlayerCentre + transform.forward);
        leftHand.Value = ItemData.Empty;
    }

    private void ForcedropRight()
    {
        if (rightHand.Value.IsValid && ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour != null)
        {
            rightHand.Value = ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour.OnDrop(rightHand.Value, HandSideEnum.Right);
        }
        SpawnItemWorldRPC(rightHand.Value, Player.GetLocalPlayerCentre + transform.forward);
        rightHand.Value = ItemData.Empty;
        hotbar[currEquippedSlot] = rightHand.Value; // Update hotbar with new item
    }

    private IEnumerator DropLeftItem()
    {
        busy = true;
        LeftHandOb.DOLocalMove(new Vector3(initialLeftHandPos.x, -0.5f, initialLeftHandPos.z), 0.3f);
        yield return new WaitForSeconds(0.3f);
        if (leftHand.Value.IsValid && ItemDatabase.GetItem(leftHand.Value.ID.ToString()).ItemBehaviour != null)
        {
            leftHand.Value = ItemDatabase.GetItem(leftHand.Value.ID.ToString()).ItemBehaviour.OnDrop(leftHand.Value, HandSideEnum.Left);
        }
        SpawnItemWorldRPC(leftHand.Value, Player.GetLocalPlayerCentre + transform.forward);
        leftHand.Value = ItemData.Empty;
        UpdateLefthandVisuals();
        LeftHandOb.localPosition = initialLeftHandPos;
        busy = false;
    }

    private IEnumerator DropRightItem()
    {
        busy = true;
        RightHandOb.DOLocalMove(new Vector3(initialRightHandPos.x, -0.5f, initialRightHandPos.z), 0.3f);
        yield return new WaitForSeconds(0.3f);
        if (rightHand.Value.IsValid && ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour != null)
        {
            rightHand.Value = ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour.OnDrop(rightHand.Value, HandSideEnum.Right);
        }
        SpawnItemWorldRPC(rightHand.Value, Player.GetLocalPlayerCentre + transform.forward);
        rightHand.Value = ItemData.Empty;
        hotbar[currEquippedSlot] = rightHand.Value; // Update hotbar with new item
        UpdateRighthandVisuals();
        RightHandOb.localPosition = initialRightHandPos;
        busy = false;
    }

    private IEnumerator SwapHandsCoroutine()
    {
        busy = true;
        LeftHandOb.gameObject.SetActive(true);
        RightHandOb.gameObject.SetActive(true);
        RIghthandItemMesh.gameObject.SetActive(rightHand.Value.IsValid);
        LefthandItemMesh.gameObject.SetActive(leftHand.Value.IsValid);
        LeftHandOb.DOLocalMove(new Vector3(-0.05f, initialLeftHandPos.y, initialLeftHandPos.z), 0.3f);
        RightHandOb.DOLocalMove(new Vector3(0.05f, initialRightHandPos.y, initialRightHandPos.z), 0.3f);
        yield return new WaitForSeconds(0.3f);
        var temp = rightHand.Value;
        rightHand.Value = leftHand.Value;
        leftHand.Value = temp;
        if (rightHand.Value.IsValid && ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour != null)
        {
            rightHand.Value = ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour.OnHandsSwapped(rightHand.Value, HandSideEnum.Right);
        }
        if (leftHand.Value.IsValid && ItemDatabase.GetItem(leftHand.Value.ID.ToString()).ItemBehaviour != null)
        {
            leftHand.Value = ItemDatabase.GetItem(leftHand.Value.ID.ToString()).ItemBehaviour.OnHandsSwapped(leftHand.Value, HandSideEnum.Left);
        }
        hotbar[currEquippedSlot] = rightHand.Value; // Update hotbar with new item
        UpdateLefthandVisuals();
        UpdateRighthandVisuals();
        LeftHandOb.gameObject.SetActive(true);
        RightHandOb.gameObject.SetActive(true);
        RIghthandItemMesh.gameObject.SetActive(rightHand.Value.IsValid);
        LefthandItemMesh.gameObject.SetActive(leftHand.Value.IsValid);
        LeftHandOb.DOLocalMove(initialLeftHandPos, 0.3f);
        RightHandOb.DOLocalMove(initialRightHandPos, 0.3f);
        yield return new WaitForSeconds(0.3f);
        RIghthandItemMesh.gameObject.SetActive(true);
        LefthandItemMesh.gameObject.SetActive(true);
        LeftHandOb.gameObject.SetActive(leftHand.Value.IsValid);
        RightHandOb.gameObject.SetActive(rightHand.Value.IsValid);
        busy = false;
    }

    private void TryCraft(string a, string b)
    {
        if (a == string.Empty || b == string.Empty) return;

        if (ItemDatabase.GetCraft(a, b, out string result))
        {
            rightHand.Value = new ItemData(result, ItemDatabase.GetItem(result).BaseSavedData);
            if (rightHand.Value.IsValid && ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour != null)
            {
                rightHand.Value = ItemDatabase.GetItem(rightHand.Value.ID.ToString()).ItemBehaviour.OnWasCrafted(rightHand.Value);
            }
            hotbar[currEquippedSlot] = rightHand.Value; // Update hotbar with new item
            leftHand.Value = ItemData.Empty; // Clear left hand
        }
    }

    private void Died()
    {
        foreach(var item in hotbar)
        {
            if (item.IsValid)
            {
                var rand = Random.insideUnitCircle*0.35f;
                SpawnItemWorldRPC(item, Player.GetLocalPlayerCentre + (Vector3.up * 0.5f) + new Vector3(rand.x, 0, rand.y));
            }
        }

        foreach (var item in backpack)
        {
            if (item.IsValid)
            {
                var rand = Random.insideUnitCircle * 0.35f;
                SpawnItemWorldRPC(item, Player.GetLocalPlayerCentre + (Vector3.up * 0.5f) + new Vector3(rand.x, 0, rand.y));
            }
        }

        if (leftHand.Value.IsValid)
        {
            var rand = Random.insideUnitCircle * 0.35f;
            SpawnItemWorldRPC(leftHand.Value, Player.GetLocalPlayerCentre + (Vector3.up * 0.5f) + new Vector3(rand.x, 0, rand.y));
        }
    }

    [Rpc(SendTo.Server)]
    private void SpawnItemWorldRPC(ItemData item, Vector3 position)
    {
        // Spawn logic should be synced
        var go = Instantiate(ItemDatabase.GetItem(item.ID.ToString()).ItemPrefab, position, Quaternion.identity);
        go.NetworkObject.Spawn();
        go.InitSavedData(item.SavedData);
    }

    public ItemData StealFromBackpack()
    {
        if (backpack.Count > 0)
        {
            var val = backpack[^1];
            if (val.IsValid && ItemDatabase.GetItem(val.ID.ToString()).ItemBehaviour != null)
            {
                val = ItemDatabase.GetItem(val.ID.ToString()).ItemBehaviour.OnWasStolen(val);
            }
            backpack.RemoveAt(backpack.Count - 1);
            return val;
        }

        return ItemData.Empty;
    }

    public void StealItem()
    {
        StealItemRPC();
    }

    [Rpc(SendTo.Owner)]
    private void StealItemRPC()
    {
        var item = StealFromBackpack();
        if (item.IsValid)
        {
            SpawnItemWorldRPC(item, Player.GetLocalPlayerCentre + transform.forward);
        }
    }


}
