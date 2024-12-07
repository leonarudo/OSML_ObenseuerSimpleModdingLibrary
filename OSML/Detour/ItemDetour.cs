using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using FullSerializer;
using NPC;

namespace OSML.Detour
{
    public static class ItemDetour
    {
        #region ItemDatabase.DeSerialize

        private static readonly fsSerializer JSON_serializer = new fsSerializer();

        public static void PatchItemDBDeSerialize()
        {
            Debug.Log("[OSML] Trying to detour ItemDatabase.DeSerialize()!");

            DetourUtility.TryDetourFromTo(
                src: typeof(ItemDatabase).GetMethod("DeSerialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance),
                dst: typeof(ItemDetour).GetMethod("NewDeSerialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            );
        }

        static object NewDeSerialize(Type type, string serializedState)
        {
            fsData data = fsJsonParser.Parse(serializedState);
            object result = null;
            JSON_serializer.TryDeserialize(data, type, ref result).AssertSuccessWithoutWarnings();

            // OSML
            if(type == typeof(List<Item>))
            {
                List<Item> database = result as List<Item>;

                foreach (var modItems in PublicVars.itemConfigPaths)
                {
                    List<Item> modItemDB = null;

                    try
                    {
                        Debug.Log($"[OSML] Loading '{modItems.Key}'s Item Database...");

                        fsData mod_data = fsJsonParser.Parse(File.ReadAllText(modItems.Value));
                        object mod_result = null;
                        JSON_serializer.TryDeserialize(mod_data, type, ref mod_result).AssertSuccessWithoutWarnings();

                        modItemDB = mod_result as List<Item>;

                        // Adding 'OSML ModItem' to the Item category as an signature/identifier
                        foreach (Item i in modItemDB)
                        {
                            string[] updated_categories = new string[i.Categories.Length + 1];
                            for(int j = 0;  j < i.Categories.Length; j++)
                            {
                                updated_categories[j] = i.Categories[j];
                            }
                            updated_categories[i.Categories.Length - 1] = "OSML ModItem";
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[OSML] Error while loading '{modItems.Key}'s Item Database!\nException: {ex}");
                    }
                    finally
                    {
                        database.AddRange(modItemDB);

                        Debug.Log($"[OSML] {modItemDB.Count} Items loaded!");
                    }
                }

                foreach(var dbHandler in PublicVars.itemDatabaseHandlers)
                {
                    database = dbHandler.Value.Invoke(database);
                }

                return database;
            }

            // END

            return result;
        }

        #endregion

        #region ItemOperations.SetCollectibleItemValues

        public static void PatchSetCollectibleItemValues()
        {
            Debug.Log("[OSML] Trying to detour ItemOperations.SetCollectibleItemValues()!");

            DetourUtility.TryDetourFromTo(
                src: typeof(ItemOperations).GetMethod("SetCollectibleItemValues", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance),
                dst: typeof(ItemDetour).GetMethod("NewSetCollectibleItemValues", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            );
        }

        static void NewSetCollectibleItemValues(ItemStack item, GameObject gameObject)
        {
            CollectibleItem component = gameObject.GetComponent<CollectibleItem>();
            if (component != null)
            {
                // OSML

                component.Item = item.itemReference;

                // END

                component.StartExpirationTimer(null);
                component.amount = item.itemAmount;
                component.owner = new NPCReference(item.ownerId);
                component.meta = item.Meta;
                component.useSpawnInfo = false;
                component.GUID = "";
                component.SetItemMeta(null);
                FurniturePlaceable component2 = gameObject.GetComponent<FurniturePlaceable>();
                if (component2)
                {
                    ItemOperations.SetCollectibleItemFurniturePlaceable(component, component2);
                    return;
                }
            }
            else if (gameObject.GetComponent<CollectibleCoin>() != null)
            {
                gameObject.GetComponent<CollectibleCoin>().StartExpirationTimer(null);
                gameObject.GetComponent<CollectibleCoin>().Amount = (float)item.itemAmount;
            }
        }

        #endregion

        #region Item.ItemAppearance

        public static void PatchItemAppearanceALL()
        {
            Debug.Log("[OSML] Trying to detour Item.ItemAppearance.LoadSprite()!");

            DetourUtility.TryDetourFromTo(
                src: DetourUtility.MethodInfoForMethodCall(() => default(Item.ItemAppearance).LoadSprite()),
                dst: DetourUtility.MethodInfoForMethodCall(() => NewLoadSprite(default))
            );

            Debug.Log("[OSML] Trying to detour Item.ItemAppearance.Loadprefab()!");

            DetourUtility.TryDetourFromTo(
                src: DetourUtility.MethodInfoForMethodCall(() => default(Item.ItemAppearance).Loadprefab(default)),
                dst: DetourUtility.MethodInfoForMethodCall(() => NewLoadprefab(default, default))
            );
        }

        static Item.ItemAppearance NewLoadSprite(this Item.ItemAppearance iap)
        {
            // OSML

            if(!string.IsNullOrEmpty(iap.SpritePath))
            {
                if (iap.SpritePath.StartsWith("OSML#"))
                {
                    string path = Path.Combine(Assembly.GetAssembly(typeof(ObenseuerSimpleModdingLibrary)).Location.Substring(0, Assembly.GetAssembly(typeof(ObenseuerSimpleModdingLibrary)).Location.Length - 13), iap.SpritePath.Substring(5));

                    if (File.Exists(path))
                    {
                        Texture2D tex = new Texture2D(1, 1);

                        try
                        {
                            tex.LoadImage(File.ReadAllBytes(path));

                            iap.Sprite = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100.0f);
                            return null;
                        }
                        catch (Exception ex)
                        {
                            return null;
                        }
                    }
                }
            }

            // END

            if (!string.IsNullOrEmpty(iap.SpritePath))
            {
                iap.Sprite = Resources.Load<Sprite>(iap.SpritePath);
            }

            return null;
        }

        static Item.ItemAppearance NewLoadprefab(this Item.ItemAppearance iap,Item item)
        {
            // OSML

            if(!string.IsNullOrEmpty(iap.PrefabPath))
            {
                if (iap.PrefabPath.StartsWith("OSML#"))
                {
                    string path = Path.Combine(Assembly.GetAssembly(typeof(ObenseuerSimpleModdingLibrary)).Location.Substring(0, Assembly.GetAssembly(typeof(ObenseuerSimpleModdingLibrary)).Location.Length - 13), iap.PrefabPath.Substring(5));
                    path = path.Substring(0, path.Length - 3);

                    if (File.Exists(path + "png") && File.Exists(path + "obj"))
                    {
                        iap.Prefab = ItemCreator.ItemPrefabFromOBJ(path + "obj", path + "png", item.Title);
                        if (iap.Prefab == null)
                        {
                            iap.Prefab = Resources.Load<GameObject>("Prefabs/Items/ERROR");
                        }
                    }
                    else if(File.Exists(path + "jpg") && File.Exists(path + "obj"))
                    {
                        iap.Prefab = ItemCreator.ItemPrefabFromOBJ(path + "obj", path + "jpg", item.Title);
                        if (iap.Prefab == null)
                        {
                            iap.Prefab = Resources.Load<GameObject>("Prefabs/Items/ERROR");
                        }
                    }

                    return null;
                }
            }

            if(!string.IsNullOrEmpty(iap.PrefabPathMany))
            {
                if (iap.PrefabPathMany.StartsWith("OSML#"))
                {
                    if (iap.PrefabPathMany.Length == 5) iap.PrefabMany = iap.Prefab;

                    string pathMany = Path.Combine(Assembly.GetAssembly(typeof(ObenseuerSimpleModdingLibrary)).Location.Substring(0, Assembly.GetAssembly(typeof(ObenseuerSimpleModdingLibrary)).Location.Length - 13), iap.PrefabPathMany.Substring(5));
                    pathMany = pathMany.Substring(0, pathMany.Length - 3);

                    if (File.Exists(pathMany + "png") && File.Exists(pathMany + "obj"))
                    {
                        iap.Prefab = ItemCreator.ItemPrefabFromOBJ(pathMany + "obj", pathMany + "png", item.Title);
                        if (iap.Prefab == null)
                        {
                            iap.Prefab = Resources.Load<GameObject>("Prefabs/Items/ERROR");
                        }
                    }
                    else if (File.Exists(pathMany + "jpg") && File.Exists(pathMany + "obj"))
                    {
                        iap.Prefab = ItemCreator.ItemPrefabFromOBJ(pathMany + "obj", pathMany + "jpg", item.Title);
                        if (iap.Prefab == null)
                        {
                            iap.Prefab = Resources.Load<GameObject>("Prefabs/Items/ERROR");
                        }
                    }

                    return null;
                }
            }

            // END

            if (!string.IsNullOrEmpty(iap.PrefabPath))
            {
                iap.Prefab = Resources.Load<GameObject>(iap.PrefabPath);
                if (iap.Prefab == null)
                {
                    iap.PrefabPath = Item.StripPath(iap.PrefabPath);
                    iap.Prefab = Resources.Load<GameObject>(iap.PrefabPath);
                    if (iap.Prefab == null)
                    {
                        iap.Prefab = Resources.Load<GameObject>("Prefabs/Items/ERROR");
                    }
                }
            }
            else
            {
                iap.Prefab = Resources.Load<GameObject>("Prefabs/Items/" + item.Title);
                if (iap.Prefab == null)
                {
                    iap.Prefab = Resources.Load<GameObject>("Prefabs/Items/ERROR");
                }
            }
            if (!string.IsNullOrEmpty(iap.PrefabPathMany))
            {
                iap.PrefabMany = Resources.Load<GameObject>(iap.PrefabPathMany);
                if (iap.PrefabMany == null)
                {
                    iap.PrefabPathMany = Item.StripPath(iap.PrefabPathMany);
                    iap.PrefabMany = Resources.Load<GameObject>(iap.PrefabPathMany);
                    if (iap.PrefabMany == null)
                    {
                        iap.PrefabMany = iap.Prefab;
                        return null;
                    }
                }
            }
            else
            {
                iap.PrefabMany = iap.Prefab;
            }

            return null;
        }

        #endregion
    }
}
