﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEditor;
using System;

[CreateAssetMenu(menuName = "Wave Function Collapse/Module Data", fileName = "modules.asset")]
public class ModuleData : ScriptableObject, ISerializationCallbackReceiver {
	public static Module[] Current;

	public Module[] Modules;
	
	public void CreateModules() {
		this.Modules = ModuleData.CreateModules(true).ToArray();
		EditorUtility.SetDirty(this);
	}

#if UNITY_EDITOR
	public void SimplifyNeighborData() {
		ModuleData.Current = this.Modules;
		const int height = 12;
		int count = 0;
		var center = new Vector3i(0, height / 2, 0);

		int p = 0;
		foreach (var module in this.Modules) {
			var map = new InfiniteMap(height);
			var slot = map.GetSlot(center);
			try {
				slot.Collapse(module);
			}
			catch (CollapseFailedException exception) {
				throw new InvalidOperationException("Module " + module.Name + " creates a failure at relative position " + (exception.Slot.Position - center) + ".");
			}
			for (int direction = 0; direction < 6; direction++) {
				var neighbor = slot.GetNeighbor(direction);
				int unoptimizedNeighborCount = module.PossibleNeighbors[direction].Length;
				module.PossibleNeighbors[direction] = module.PossibleNeighbors[direction].Where(m => neighbor.Modules.Contains(m)).ToArray();
				count += unoptimizedNeighborCount - module.PossibleNeighbors[direction].Length;
			}
			module.Cloud = new Dictionary<Vector3i, ModuleSet>();
			foreach (var cloudSlot in map.GetAllSlots()) {
				if (cloudSlot.Position.Equals(center)) {
					continue;
				}
				if (cloudSlot.Modules.Full) {
					continue;
				}
				module.Cloud[cloudSlot.Position - center] = cloudSlot.Modules;
			}
			Debug.Log(module.Cloud.Keys.Count);
			p++;
			EditorUtility.DisplayProgressBar("Simplifying... " + count, module.Name, (float)p / this.Modules.Length);
		}
		Debug.Log("Removed " + count + " impossible neighbors.");
		EditorUtility.ClearProgressBar();
		EditorUtility.SetDirty(this);
	}
#endif

	public static List<Module> CreateModules(bool respectNeigborExclusions) {
		int count = 0;
		var modules = new List<Module>();

		var prototypes = ModulePrototype.GetAll().ToArray();

		var scenePrototype = new Dictionary<Module, ModulePrototype>();

		for (int i = 0; i < prototypes.Length; i++) {
			var prototype = prototypes[i];
			for (int face = 0; face < 6; face++) {
				if (prototype.Faces[face].ExcludedNeighbours == null) {
					prototype.Faces[face].ExcludedNeighbours = new ModulePrototype[0];
				}
			}

			var prefab = PrefabUtility.CreatePrefab("Assets/ModulePrefabs/" + prototype.gameObject.name + ".prefab", prototype.gameObject);

			for (int rotation = 0; rotation < 4; rotation++) {
				if (rotation == 0 || !prototype.CompareRotatedVariants(0, rotation)) {
					var module = new Module(prefab, rotation, count);
					modules.Add(module);
					scenePrototype[module] = prototype;
					count++;
				}
			}

			EditorUtility.DisplayProgressBar("Creating module prototypes...", prototype.gameObject.name, (float)i / prototypes.Length);
		}

		foreach (var module in modules) {
			module.PossibleNeighbors = new Module[6][];
			for (int direction = 0; direction < 6; direction++) {
				var face = scenePrototype[module].Faces[Orientations.Rotate(direction, module.Rotation)];
				module.PossibleNeighbors[direction] = modules
					.Where(neighbor => module.Fits(direction, neighbor)
						&& (!respectNeigborExclusions || (
							!face.ExcludedNeighbours.Contains(scenePrototype[neighbor])
							&& !scenePrototype[neighbor].Faces[Orientations.Rotate((direction + 3) % 6, neighbor.Rotation)].ExcludedNeighbours.Contains(scenePrototype[module]))
							&& (!face.EnforceWalkableNeighbor || scenePrototype[neighbor].Faces[Orientations.Rotate((direction + 3) % 6, neighbor.Rotation)].Walkable)
							&& (face.Walkable || !scenePrototype[neighbor].Faces[Orientations.Rotate((direction + 3) % 6, neighbor.Rotation)].EnforceWalkableNeighbor))
					)
					.ToArray();
			}
		}
		EditorUtility.ClearProgressBar();

		return modules;
	}

	public void OnBeforeSerialize() { }

	public void OnAfterDeserialize() {
		if (this.Modules != null && this.Modules.Length != 0) {
			foreach (var module in this.Modules) {
				module.DeserializeNeigbors(this.Modules);
			}
		}
	}
}
