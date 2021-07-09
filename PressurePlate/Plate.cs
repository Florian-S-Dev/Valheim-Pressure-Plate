﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PressurePlate {
    public class Plate : MonoBehaviour, Hoverable, Interactable {
        public GameObject plate;
        public bool isPressed;
        public Player lastPlayer;
        private float pressCooldown;
        public EffectList pressEffects = new EffectList();
        public EffectList releaseEffects = new EffectList();
        public ZNetView ZNetView;

        private void Awake() {
            isPressed = FindPlayerInRange();
            ZNetView = GetComponent<ZNetView>();
        }

        private void FixedUpdate() {
            if (!ZNetView.IsValid()) {
                return; //wait for network spawn
            }

            bool wasPressed = isPressed;
            bool newPressed = FindPlayerInRange();

            if (newPressed) {
                List<DoorPowerState> doors = DoorPowerState.FindDoorsInPlateRange(transform.position);

                float? maxTime = doors.Max(i => i.GetDoorConfig()?.openTime);
                pressCooldown = maxTime ?? Plugin.plateOpenDelay.Value;

                isPressed = true;
            } else {
                if (pressCooldown > 0) {
                    pressCooldown -= Time.fixedDeltaTime;
                    isPressed = true;
                } else {
                    isPressed = false;
                }
            }

            bool stateChange = isPressed != wasPressed;

            Vector3 pos = isPressed ? new Vector3(0f, -0.025f, 0f) : new Vector3(0f, 0.05f, 0f);
            plate.transform.localPosition = pos;

            if (stateChange) {
                if (isPressed) {
                    pressEffects.Create(transform.position, Quaternion.identity);
                } else {
                    releaseEffects.Create(transform.position, Quaternion.identity);
                }

                List<DoorPowerState> doors = DoorPowerState.FindDoorsInPlateRange(transform.position);

                foreach (DoorPowerState door in doors) {
                    if (isPressed) {
                        door.AddPoweringPlate(this);
                    } else {
                        door.RemovePoweringPlate(this);
                    }
                }
            }

            if (lastPlayer == null) return;

            if (stateChange || isPressed) {
                List<DoorPowerState> doors = DoorPowerState.FindDoorsInPlateRange(transform.position);
                foreach (DoorPowerState door in doors) {
                    if (isPressed) {
                        // always open the door if the plate is pressed
                        door.Open(lastPlayer, this);
                    } else {
                        // only close the door if this is the last plate powering it
                        if (door.GetPoweringPlates().Count(i => i != this) > 0) continue;

                        door.Close(lastPlayer, this);
                    }
                }
            }
        }

        private bool FindPlayerInRange() {
            Player player = Player.GetAllPlayers().Find(i => InRange(i.transform.position, Plugin.playerPlateRadiusXZ.Value, Plugin.playerPlateRadiusY.Value));

            if (player != null) {
                lastPlayer = player;
                return true;
            }

            return false;
        }

        private bool InRange(Vector3 target, float rangeXZ, float rangeY) {
            Vector3 delta = transform.position - target;
            bool inXZ = new Vector3(delta.x, 0, delta.z).sqrMagnitude <= rangeXZ * rangeXZ;
            bool inY = Mathf.Abs(delta.y) <= rangeY;
            return inXZ && inY;
        }

        public string GetHoverText() {
            if (!ZNetView.IsValid()) return "";

            string text = ""; // "$Public (ignore wards): ";
            bool plateIsPublic = ZNetView.GetZDO().GetBool("pressure_plate_is_public");

            if (plateIsPublic) {
                text += "$Public\n";
                text += "[<color=yellow><b>$KEY_Use</b></color>] $Activate";
            } else {
                text += "$Private\n";
                text += "[<color=yellow><b>$KEY_Use</b></color>] $Deactivate";
            }

            return Localization.instance.Localize(text);
        }

        public string GetHoverName() {
            Debug.Log("name: " + name +", " + GetComponent<Piece>().m_name);
            return name;
        }

        public bool Interact(Humanoid user, bool hold) {
            if (hold) {
                Debug.Log("hold");
                return false;
            }

            if (!PrivateArea.CheckAccess(base.transform.position)) {
                Debug.Log("no Access");
                return true;
            }

            bool plateIsPublic = ZNetView.GetZDO().GetBool("pressure_plate_is_public");
            ZNetView.GetZDO().Set("pressure_plate_is_public", !plateIsPublic);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) {
            return false;
        }
    }
}
