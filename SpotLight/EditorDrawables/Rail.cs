﻿using BYAML;
using GL_EditorFramework;
using GL_EditorFramework.EditorDrawables;
using OpenTK;
using Spotlight.Level;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BYAML.ByamlNodeWriter;
using static GL_EditorFramework.EditorDrawables.EditorSceneBase;
using static GL_EditorFramework.EditorDrawables.EditorSceneBase.PropertyCapture;

namespace Spotlight.EditorDrawables
{
    public class Rail : Path<RailPoint>, I3dWorldObject
    {
        public Dictionary<string, dynamic> Properties { get; private set; } = null;

        public override string ToString()
        {
            return ClassName.ToString();
        }

        protected static List<RailPoint> RailPointsFromRailPointsEntry(ByamlIterator.DictionaryEntry railPointsEntry)
        {
            List<RailPoint> pathPoints = new List<RailPoint>();

            foreach (ByamlIterator.ArrayEntry pointEntry in railPointsEntry.IterArray())
            {
                Vector3 pos = new Vector3();
                Vector3 cp1 = new Vector3();
                Vector3 cp2 = new Vector3();

                var properties = new Dictionary<string, dynamic>();

                foreach (ByamlIterator.DictionaryEntry entry in pointEntry.IterDictionary())
                {
                    if (entry.Key == "Comment" ||
                        entry.Key == "Id" ||
                        entry.Key == "IsLinkDest" ||
                        entry.Key == "LayerConfigName" ||
                        entry.Key == "Links" ||
                        entry.Key == "ModelName" ||
                        entry.Key == "Rotate" ||
                        entry.Key == "Scale" ||
                        entry.Key == "UnitConfig" ||
                        entry.Key == "UnitConfigName"

                        )
                        continue;

                    dynamic _data = entry.Parse();
                    if (entry.Key == "Translate")
                    {
                        pos = new Vector3(
                            _data["X"] / 100f,
                            _data["Y"] / 100f,
                            _data["Z"] / 100f
                        );
                    }
                    else if (entry.Key == "ControlPoints")
                    {
                        cp1 = new Vector3(
                            _data[0]["X"] / 100f,
                            _data[0]["Y"] / 100f,
                            _data[0]["Z"] / 100f
                        );

                        cp2 = new Vector3(
                            _data[1]["X"] / 100f,
                            _data[1]["Y"] / 100f,
                            _data[1]["Z"] / 100f
                        );
                    }
                    else
                        properties.Add(entry.Key, _data);
                }

                pathPoints.Add(new RailPoint(pos, cp1 - pos, cp2 - pos, properties));
            }

            return pathPoints;
        }

        /// <summary>
        /// Id of this object
        /// </summary>
        public string ID { get; }

        [Undoable]
        public bool IsLadder { get; set; }

        [Undoable]
        public string ClassName { get; set; }

        readonly string comment = null;

        public Rail(in LevelIO.ObjectInfo info, SM3DWorldZone zone, out bool loadLinks)
        {
            pathPoints = RailPointsFromRailPointsEntry(info.PropertyEntries["RailPoints"]);

            ID = info.ID;
            if (zone != null)
                zone.SubmitRailID(ID);

            ClassName = info.ClassName;

            comment = info.Comment;

            Properties = new Dictionary<string, dynamic>();

            foreach (var propertyEntry in info.PropertyEntries)
            {
                switch (propertyEntry.Key)
                {
                    case "IsLadder":
                        IsLadder = propertyEntry.Value.Parse();
                        continue;
                    case "IsClosed":
                        Closed = propertyEntry.Value.Parse();
                        continue;
                    case "RailType":
                    case "RailPoints":
                        continue;
                    default:
                        Properties.Add(propertyEntry.Key, propertyEntry.Value.Parse());
                        continue;
                }
            }

            zone?.SubmitRailID(ID);

            loadLinks = false; //We don't expect Rails to have Links
        }
        /// <summary>
        /// Creates a new rail for 3DW
        /// </summary>
        /// <param name="pathPoints">List of Path Points to use in this rail</param>
        /// <param name="iD">ID Of the rail</param>
        /// <param name="isClosed">Is the path closed?</param>
        /// <param name="isLadder">Unknown</param>
        /// <param name="isReverseCoord">Reverses the order the rails are in</param>
        /// <param name="className"></param>
        public Rail(List<RailPoint> railPoints, string iD, string className, bool isClosed, bool isLadder, Dictionary<string, dynamic> properties, SM3DWorldZone zone)
        {
            ID = iD;
            Closed = isClosed;
            IsLadder = isLadder;
            Properties = properties;
            ClassName = className;

            pathPoints = railPoints;

            foreach (var point in pathPoints)
                SetupPoint(point);

            zone?.SubmitRailID(ID);
        }

        protected override void SetupPoint(RailPoint point)
        {
            if(Program.ParameterDB.RailParameters.TryGetValue(ClassName, out Database.RailParam railParam))
                Database.ObjectParameterDatabase.AddToProperties(railParam.PointProperties, point.Properties);
        }

        #region I3DWorldObject implementation
        /// <summary>
        /// All places where this object is linked to
        /// </summary>
        public List<(string, I3dWorldObject)> LinkDestinations { get; } = new List<(string, I3dWorldObject)>();

        public Dictionary<string, List<I3dWorldObject>> Links { get => null; set { } } //We don't expect Rails to have Links

        public void Save(HashSet<I3dWorldObject> alreadyWrittenObjs, ByamlNodeWriter writer, DictionaryNode objNode, bool isLinkDest = false)
        {
            objNode.AddDynamicValue("Comment", null);
            objNode.AddDynamicValue("Id", ID);
            objNode.AddDynamicValue("IsClosed", Closed);
            objNode.AddDynamicValue("IsLadder", IsLadder);

            objNode.AddDynamicValue("IsLinkDest", isLinkDest);
            objNode.AddDynamicValue("LayerConfigName", "Common");

            alreadyWrittenObjs.Add(this);
            
            objNode.AddDictionaryNodeRef("Links", writer.CreateDictionaryNode(), true); //We don't expect Rails to have Links

            objNode.AddDynamicValue("ModelName", null);

            #region Save RailPoints
            ArrayNode railPointsNode = writer.CreateArrayNode();

            int i = 0;
            foreach (RailPoint point in PathPoints)
            {
                DictionaryNode pointNode = writer.CreateDictionaryNode();

                pointNode.AddDynamicValue("Comment", null);

                pointNode.AddDynamicValue("ControlPoints", new List<dynamic>()
                {
                    LevelIO.Vector3ToDict(point.ControlPoint1 + point.Position, 100f),
                    LevelIO.Vector3ToDict(point.ControlPoint2 + point.Position, 100f)
                });

                pointNode.AddDynamicValue("Id", $"{ID}/{i}");
                pointNode.AddDynamicValue("IsLinkDest", isLinkDest);
                pointNode.AddDynamicValue("LayerConfigName", "Common");

                pointNode.AddDictionaryNodeRef("Links", writer.CreateDictionaryNode(), true); //We don't expect Points to have Links either

                pointNode.AddDynamicValue("ModelName", null);

                pointNode.AddDynamicValue("Rotate", LevelIO.Vector3ToDict(Vector3.Zero), true);
                pointNode.AddDynamicValue("Scale", LevelIO.Vector3ToDict(Vector3.One), true);
                pointNode.AddDynamicValue("Translate", LevelIO.Vector3ToDict(point.Position, 100f), true);
                
                pointNode.AddDynamicValue("UnitConfig", ObjectUtils.CreateUnitConfig("Point"), true);
                
                pointNode.AddDynamicValue("UnitConfigName", "Point");

                if (point.Properties.Count != 0)
                {
                    foreach (var property in point.Properties)
                    {
                        if (property.Value is string && property.Value == "")
                            pointNode.AddDynamicValue(property.Key, null, true);
                        else
                            pointNode.AddDynamicValue(property.Key, property.Value, true);
                    }
                }

                railPointsNode.AddDictionaryNodeRef(pointNode, true);

                i++;
            }

            objNode.AddArrayNodeRef("RailPoints", railPointsNode);
            #endregion

            objNode.AddDynamicValue("RailType", "Bezier");

            objNode.AddDynamicValue("Rotate", LevelIO.Vector3ToDict(Vector3.Zero), true);
            objNode.AddDynamicValue("Scale", LevelIO.Vector3ToDict(Vector3.One), true);
            objNode.AddDynamicValue("Translate", LevelIO.Vector3ToDict(PathPoints[0].Position, 100f), true);

            objNode.AddDynamicValue("UnitConfig", ObjectUtils.CreateUnitConfig(ClassName), true);

            objNode.AddDynamicValue("UnitConfigName", ClassName);

            if (Properties.Count != 0)
            {
                foreach (KeyValuePair<string, dynamic> property in Properties)
                {
                    if (property.Value is string && property.Value == "")
                        objNode.AddDynamicValue(property.Key, null, true);
                    else
                        objNode.AddDynamicValue(property.Key, property.Value, true);
                }
            }
        }

        public virtual Vector3 GetLinkingPoint(SM3DWorldScene editorScene)
        {
            return PathPoints[0]?.GetLinkingPoint(editorScene) ?? Vector3.Zero;
        }

        public void UpdateLinkDestinations_Clear()
        {
            LinkDestinations.Clear();
        }

        public void UpdateLinkDestinations_Populate()
        {
            //We don't expect Rails to have Links
        }

        public void AddLinkDestination(string linkName, I3dWorldObject linkingObject)
        {
            LinkDestinations.Add((linkName, linkingObject));
        }

        public void DuplicateSelected(Dictionary<I3dWorldObject, I3dWorldObject> duplicates, SM3DWorldZone destZone, ZoneTransform? zoneToZoneTransform = null)
        {
            LinkDestinations.Clear();

            bool anyPointsSelected = false;
            foreach (PathPoint point in pathPoints)
            {
                if (point.Selected)
                    anyPointsSelected = true;
            }


            if(!anyPointsSelected)
                return;

            List<RailPoint> newPoints = new List<RailPoint>();

            foreach (RailPoint point in pathPoints)
            {
                if (point.Selected)
                {
                    //copy point properties
                    Dictionary<string, dynamic> newPointProperties = new Dictionary<string, dynamic>();

                    foreach (var property in point.Properties)
                        newPointProperties.Add(property.Key, property.Value);

                    newPoints.Add(new RailPoint(point.Position, point.ControlPoint1, point.ControlPoint2, newPointProperties));
                }
            }

            //copy path properties
            Dictionary<string, dynamic> newPathProperties = new Dictionary<string, dynamic>();

            foreach (var property in Properties)
                newPathProperties.Add(property.Key, property.Value);

            duplicates[this] = new Rail(newPoints, destZone?.NextRailID(), ClassName, Closed, IsLadder, newPathProperties, destZone);

#if ODYSSEY
            duplicates[this].ScenarioBitField = ScenarioBitField;
#endif
        }

        public void LinkDuplicates(SM3DWorldScene.DuplicationInfo duplicationInfo, bool allowKeepLinksOfDuplicate)
        {
            //We don't expect Rails to have Links
        }

        public bool TryGetObjectList(SM3DWorldZone zone, out ObjectList objList)
        {
            return zone.ObjLists.TryGetValue("Map_Rails", out objList);
        }

        public void AddToZoneBatch(ZoneRenderBatch zoneBatch)
        {
            //TODO figure out if this is needed or not
        }

#if ODYSSEY
        public ushort ScenarioBitField { get; set; } = 0;
#endif

        #endregion


        public override bool TrySetupObjectUIControl(EditorSceneBase scene, ObjectUIControl objectUIControl)
        {
            bool any = false;

            foreach (RailPoint point in pathPoints)
                any |= point.Selected;


            if (!any)
                return false;

            

            List<RailPoint> points = new List<RailPoint>();

            foreach (RailPoint point in pathPoints)
            {
                if (point.Selected)
                    points.Add(point);
            }

            General3dWorldObject.ExtraPropertiesUIContainer pointPropertyContainer = null;

            if (points.Count == 1)
                pointPropertyContainer = new General3dWorldObject.ExtraPropertiesUIContainer(points[0].Properties, scene);

            General3dWorldObject.ExtraPropertiesUIContainer pathPropertyContainer = new General3dWorldObject.ExtraPropertiesUIContainer(Properties, scene);

            objectUIControl.AddObjectUIContainer(new RailUIContainer(this, scene, pointPropertyContainer, pathPropertyContainer), "Rail");
            
            objectUIControl.AddObjectUIContainer(pathPropertyContainer, "Rail Properties");

            if (points.Count == 1)
            {
                objectUIControl.AddObjectUIContainer(new SinglePathPointUIContainer(points[0], scene), "Rail Point");
                objectUIControl.AddObjectUIContainer(pointPropertyContainer, "Point Properties");
            }

            if (LinkDestinations.Count > 0)
                objectUIControl.AddObjectUIContainer(new General3dWorldObject.LinkDestinationsUIContainer(this, (SM3DWorldScene)scene), "Link Destinations");

            return true;
        }

        public class RailUIContainer : IObjectUIContainer
        {
            PropertyCapture? pathCapture = null;

            Rail rail;
            readonly EditorSceneBase scene;
            string[] DB_classNames;

            General3dWorldObject.ExtraPropertiesUIContainer pathPointPropertyContainer;
            General3dWorldObject.ExtraPropertiesUIContainer pathPropertyContainer;

            public RailUIContainer(Rail rail, EditorSceneBase scene, General3dWorldObject.ExtraPropertiesUIContainer pathPointPropertyContainer, General3dWorldObject.ExtraPropertiesUIContainer pathPropertyContainer)
            {
                this.rail = rail;

                this.scene = scene;

                this.pathPointPropertyContainer = pathPointPropertyContainer;
                this.pathPropertyContainer = pathPropertyContainer;

                DB_classNames = Program.ParameterDB.RailParameters.Keys.ToArray();
            }

            public void DoUI(IObjectUIControl control)
            {
                if (rail.comment != null)
                    control.TextInput(rail.comment, "Comment");

                rail.ClassName = control.DropDownTextInput("Class Name", rail.ClassName, DB_classNames, false);

                rail.IsLadder = control.CheckBox("Is Ladder", rail.IsLadder);

                rail.Closed = control.CheckBox("Closed", rail.Closed);

                if (scene.CurrentList != rail.pathPoints && control.Button("Edit Pathpoints"))
                    scene.EnterList(rail.pathPoints);
            }

            public void OnValueChangeStart()
            {
                pathCapture = new PropertyCapture(rail);
            }

            public void OnValueChanged()
            {
                scene.Refresh();
            }

            public void OnValueSet()
            {
                pathCapture?.HandleUndo(scene);

                pathCapture = null;

                scene.Refresh();
            }

            public void UpdateProperties()
            {

            }
        }
    }

    public class RailPoint : PathPoint
    {
        public override Vector3 GlobalPos
        {
            get => Vector4.Transform(new Vector4(Position, 1), SceneDrawState.ZoneTransform.PositionTransform).Xyz;
            set => Position = Vector4.Transform(new Vector4(value, 1), SceneDrawState.ZoneTransform.PositionTransform.Inverted()).Xyz;
        }

        public override Vector3 GlobalCP1
        {
            get => Vector3.Transform(ControlPoint1, SceneDrawState.ZoneTransform.RotationTransform);
            set => ControlPoint1 = Vector3.Transform(value, SceneDrawState.ZoneTransform.RotationTransform.Inverted());
        }

        public override Vector3 GlobalCP2
        {
            get => Vector3.Transform(ControlPoint2, SceneDrawState.ZoneTransform.RotationTransform);
            set => ControlPoint2 = Vector3.Transform(value, SceneDrawState.ZoneTransform.RotationTransform.Inverted());
        }

        public Dictionary<string, dynamic> Properties { get; private set; } = null;

        public RailPoint()
            : base()
        {
            Properties = new Dictionary<string, dynamic>();
        }

        public RailPoint(Vector3 position, Vector3 controlPoint1, Vector3 controlPoint2, Dictionary<string, dynamic> properties)
            : base(position, controlPoint1, controlPoint2)
        {
            Properties = properties;
        }

        public RailPoint(Vector3 position, Vector3 controlPoint1, Vector3 controlPoint2)
            : base(position, controlPoint1, controlPoint2)
        {
            Properties = new Dictionary<string, dynamic>();
        }

        public virtual Vector3 GetLinkingPoint(SM3DWorldScene editorScene)
        {
            return Selected ? editorScene.SelectionTransformAction.NewPos(GlobalPos) : GlobalPos;
        }
    }
}
