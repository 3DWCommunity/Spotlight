﻿using GL_EditorFramework;
using OpenTK;
using Spotlight.Level;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BYAML.ByamlIterator;

namespace Spotlight.EditorDrawables
{
    public static class ObjectUtils
    {
        public static Dictionary<string, dynamic> CreateUnitConfig(General3dWorldObject obj) => new Dictionary<string, dynamic>
        {
            ["DisplayName"] = obj.DisplayName,
            ["DisplayTranslate"] = LevelIO.Vector3ToDict(obj.DisplayTranslation, 100f),
            ["GenerateCategory"] = "",
            ["ParameterConfigName"] = obj.ClassName,
            ["PlacementTargetFile"] = "Map"
        };

        public static Dictionary<string, dynamic> CreateUnitConfig(string className) => new Dictionary<string, dynamic>
        {
            ["DisplayName"] = className,
            ["DisplayTranslate"] = LevelIO.Vector3ToDict(Vector3.Zero),
            ["GenerateCategory"] = "",
            ["ParameterConfigName"] = className,
            ["PlacementTargetFile"] = "Map"
        };


        public static Dictionary<string, List<I3dWorldObject>> DuplicateLinks(Dictionary<string, List<I3dWorldObject>> links)
        {
            Dictionary<string, List<I3dWorldObject>> newLinks;
            if (links != null)
            {
                newLinks = new Dictionary<string, List<I3dWorldObject>>();
                foreach (var (linkName, link) in links)
                {
                    newLinks[linkName] = new List<I3dWorldObject>();
                    foreach (I3dWorldObject obj in link)
                    {
                        newLinks[linkName].Add(obj);
                    }
                }
                return newLinks;
            }
            else
                return null;
        }

        public static Dictionary<string, dynamic> DuplicateProperties(Dictionary<string, dynamic> properties)
        {
            Dictionary<string, dynamic> newProperties = new Dictionary<string, dynamic>();

            foreach (KeyValuePair<string, dynamic> property in properties)
                newProperties[property.Key] = property.Value;

            return newProperties;
        }

        public static void LinkDuplicates(I3dWorldObject self, SM3DWorldScene.DuplicationInfo duplicationInfo, bool allowLinkCopyToOrignal)
        {
            if (self.Links != null)
            {
                bool self_isCopy = duplicationInfo.IsDuplicate(self);

                bool self_hasCopy = duplicationInfo.HasDuplicate(self);

                foreach (var (linkName, link) in self.Links)
                {
                    I3dWorldObject[] oldLink = link.ToArray();

                    //Clear Link
                    link.Clear();

                    //Populate Link
                    foreach (I3dWorldObject linked in oldLink)
                    {
                        bool linked_hasCopy = duplicationInfo.TryGetDuplicate(linked, out I3dWorldObject linked_copy);

                        
                        if (self_isCopy)
                        {
                            if (linked_hasCopy)
                            {
                                //link copy to copy
                                link.Add(linked_copy);
                            }
                            else if(allowLinkCopyToOrignal)
                            {
                                //link copy to original
                                link.Add(linked);
                            }
                        }
                        else //self is original
                        {
                            if (linked_hasCopy && !self_hasCopy)
                            {
                                //link original to original and copy
                                link.Add(linked);
                                link.Add(linked_copy);
                            }
                            else
                            {
                                //link original to original
                                link.Add(linked);
                            }
                        }
                    }
                }
            }
        }


        public static Vector3 TransformedPosition(in Vector3 position, ZoneTransform? zoneToZoneTransform)
        {
            if (zoneToZoneTransform.HasValue)
                return (new Vector4(position, 1) * zoneToZoneTransform.Value.PositionTransform).Xyz;
            else
                return position;
        }

        public static Vector3 TransformedRotation(in Vector3 rotation, ZoneTransform? zoneToZoneTransform)
        {
            if (zoneToZoneTransform.HasValue)
                return Framework.ApplyRotation(rotation, zoneToZoneTransform.Value.RotationTransform);
            else
                return rotation;
        }
    }
}
