﻿using GL_EditorFramework;
using GL_EditorFramework.EditorDrawables;
using Microsoft.WindowsAPICodePack.Dialogs;
using OpenTK;
using Spotlight.EditorDrawables;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static GL_EditorFramework.Framework;
using Spotlight.Database;
using Spotlight.Level;
using System.Threading;
using static GL_EditorFramework.EditorDrawables.EditorSceneBase;
using System.Reflection;
using Spotlight.ObjectRenderers;
using Spotlight.GUI;
using System.Drawing;
using SARCExt;
using BYAML;
using Syroot.BinaryData;

namespace Spotlight
{
    public partial class LevelEditorForm : Form
    {
        LevelParameterForm LPF;
        SM3DWorldScene currentScene;

        #region keystrokes for 3d view only
        Keys KS_AddObject = KeyStroke("Shift+A");
        Keys KS_DeleteSelected = KeyStroke("Delete");
        #endregion

        protected override bool ProcessKeyPreview(ref Message m)
        {
            Keys keyData = (Keys)(unchecked((int)(long)m.WParam)) | ModifierKeys;

            if (LevelGLControlModern.IsHovered)
            {
                if (m.Msg == 0x0100) //WM_KEYDOWN
                {
                    if (keyData == KS_AddObject)
                        AddObjectToolStripMenuItem_Click(null, null);
                    else if (keyData == KS_DeleteSelected)
                        DeleteToolStripMenuItem_Click(null, null);

                    LevelGLControlModern.PerformKeyDown(new KeyEventArgs(keyData));
                }
                else if (m.Msg == 0x0101) //WM_KEYUP
                {
                    LevelGLControlModern.PerformKeyUp(new KeyEventArgs(keyData));
                }
            }
            else if(IsExclusiveKey(keyData))
                return false;

            return base.ProcessKeyPreview(ref m);
        }

        private bool IsExclusiveKey(Keys keyData) => keyData == CopyToolStripMenuItem.ShortcutKeys ||
                keyData == PasteToolStripMenuItem.ShortcutKeys;

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (LevelGLControlModern.IsHovered &&
                (keyData & Keys.KeyCode) == Keys.Menu) //Alt key
                return true; //would trigger the menu otherwise

            else if (!LevelGLControlModern.IsHovered && IsExclusiveKey(keyData)) 
                return false; //would trigger the copy/paste action and prevent copy/paste for textboxes
            else
                return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            foreach (var tab in ZoneDocumentTabControl.Tabs)
            {
                if (tab.Document is SM3DWorldScene scene)
                    scene.CheckLocalFiles();
            }
        }

        public LevelEditorForm(DocumentTabControl.DocumentTab[] documentTabs, DocumentTabControl.DocumentTab selectedTab, QuickFavoriteControl.QuickFavorite[] quickFavorites)
            :this()
        {
            foreach (var item in documentTabs)
            {
                ZoneDocumentTabControl.AddTab(item, false);
            }

            foreach (var item in quickFavorites)
            {
                QuickFavoriteControl.AddFavorite(item);
            }

            ZoneDocumentTabControl.Select(selectedTab);

            if(currentScene!=null)
            {
                SetAppStatus(true);

                AssignSceneEvents(currentScene);
            }
        }

        public LevelEditorForm()
        {
            InitializeComponent();

            string KeyStrokeName(Keys keyData) =>
                    System.ComponentModel.TypeDescriptor.GetConverter(typeof(Keys)).ConvertToString(keyData);

            SelectAllToolStripMenuItem.ShortcutKeyDisplayString = KeyStrokeName(KS_SelectAll);
            DeselectAllToolStripMenuItem.ShortcutKeyDisplayString = KeyStrokeName(KS_DeSelectAll);

            UndoToolStripMenuItem.ShortcutKeyDisplayString = KeyStrokeName(KS_Undo);
            RedoToolStripMenuItem.ShortcutKeyDisplayString = KeyStrokeName(KS_Redo);

            AddObjectToolStripMenuItem.ShortcutKeyDisplayString = KeyStrokeName(KS_AddObject);
            DeleteToolStripMenuItem.ShortcutKeyDisplayString = KeyStrokeName(KS_DeleteSelected);

            GrowSelectionToolStripMenuItem.ShortcutKeyDisplayString = KeyStrokeName(Keys.Control|Keys.Oemplus).Replace("Oemplus","+");

            MainTabControl.SelectedTab = ObjectsTabPage;
            LevelGLControlModern.CameraDistance = 20;

            MainSceneListView.ListExited += MainSceneListView_ListExited;
            MainSceneListView.SelectionChanged += SceneListView3dWorld1_SelectionChanged;

            Localize();

            //Properties.Settings.Default.Reset(); //Used when testing to make sure the below part works

            if (Program.GamePath == "")
            {
                Program.IsProgramReady = true;
                DialogResult DR = MessageBox.Show(WelcomeMessageText, WelcomeMessageHeader, MessageBoxButtons.OK, MessageBoxIcon.Information);
                SpotlightToolStripStatusLabel.Text = StatusWelcomeMessage;
            }
            else
                SpotlightToolStripStatusLabel.Text = StatusWelcomeBackMessage;

            CommonOpenFileDialog ofd = new CommonOpenFileDialog()
            {
                Title = DatabasePickerTitle,
                IsFolderPicker = true
            };

            while (!Program.GamePathIsValid())
            {
                if (Program.GamePath != "")
                    MessageBox.Show(InvalidGamepathText, InvalidGamepathHeader, MessageBoxButtons.OK, MessageBoxIcon.Error);

                if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    Program.GamePath = ofd.FileName;
                }
                else
                {
                    Environment.Exit(0);
                }
            }

            if (!File.Exists(Program.SOPDPath))
            {
                bool Breakout = false;
                while (!Breakout)
                {
                    DialogResult DR = MessageBox.Show(DatabaseMissingText, DatabaseMissingHeader, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Error);

                    switch (DR)
                    {
                        case DialogResult.Yes:
                            Thread DatabaseGenThread = new Thread(() =>
                            {
                                LoadLevelForm LLF = new LoadLevelForm("GenDatabase", "GenDatabase");
                                LLF.ShowDialog();
                            });
                            DatabaseGenThread.Start();
                            Program.ParameterDB = new ObjectParameterDatabase();
                            Program.ParameterDB.Create(Program.BaseStageDataPath);
                            Program.ParameterDB.Save(Program.SOPDPath);
                            if (DatabaseGenThread.IsAlive)
                                LoadLevelForm.DoClose = true;
                            Breakout = true;
                            break;
                        case DialogResult.No:
                            DialogResult DR2 = MessageBox.Show(DatabaseExcludeText, DatabaseExcludeHeader, MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                            switch (DR2)
                            {
                                case DialogResult.None:
                                case DialogResult.Yes:
                                    Program.ParameterDB = null;
                                    Breakout = true;
                                    break;
                                case DialogResult.No:
                                    break;
                            }
                            break;
                        case DialogResult.None:
                        case DialogResult.Cancel:
                            Environment.Exit(1);
                            break;
                        default:
                            break;
                    }
                }
            }
            else
            {
                Program.ParameterDB = new ObjectParameterDatabase(Program.SOPDPath);
                ObjectParameterDatabase Ver = new ObjectParameterDatabase();

                if (Ver.Version > Program.ParameterDB.Version)
                {
                    bool Breakout = false;
                    while (!Breakout)
                    {
                        DialogResult DR = MessageBox.Show(string.Format(DatabaseOutdatedText, Program.ParameterDB.Version.ToString(), ObjectParameterDatabase.LatestVersion), DatabaseOutdatedHeader, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Error);

                        switch (DR)
                        {
                            case DialogResult.Yes:
                                Thread DatabaseGenThread = new Thread(() =>
                                {
                                    LoadLevelForm LLF = new LoadLevelForm("GenDatabase", "GenDatabase");
                                    LLF.ShowDialog();
                                });
                                DatabaseGenThread.Start();
                                Program.ParameterDB = new ObjectParameterDatabase();
                                Program.ParameterDB.Create(Program.BaseStageDataPath);
                                Program.ParameterDB.Save(Program.SOPDPath);
                                if (DatabaseGenThread.IsAlive)
                                    LoadLevelForm.DoClose = true;
                                Breakout = true;
                                break;
                            case DialogResult.No:
                                DialogResult DR2 = MessageBox.Show(DatabaseExcludeText, DatabaseExcludeHeader, MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                                switch (DR2)
                                {
                                    case DialogResult.None:
                                    case DialogResult.Yes:
                                        Program.ParameterDB = null;
                                        Breakout = true;
                                        break;
                                    case DialogResult.No:
                                        break;
                                }
                                break;
                            case DialogResult.None:
                            case DialogResult.Cancel:
                                Environment.Exit(1);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            if (File.Exists(Program.SODDPath))
                Program.InformationDB = new ObjectInformationDatabase(Program.SODDPath);
            else
                Program.InformationDB = new ObjectInformationDatabase();

            if (CheckForUpdates(out Version useless, false))
            {
                AboutToolStripMenuItem.BackColor = System.Drawing.Color.LawnGreen;
                CheckForUpdatesToolStripMenuItem.BackColor = System.Drawing.Color.LawnGreen;
            }

            MainSceneListView.ItemClicked += MainSceneListView_ItemClicked;
        }


        private void MainSceneListView_ListExited(object sender, ListEventArgs e)
        {
            currentScene.CurrentList = e.List;
            currentScene.SetupObjectUIControl(ObjectUIControl);
        }

        private void Scene_ListChanged(object sender, ListChangedEventArgs e)
        {
            MainSceneListView.Refresh();
        }

        private void Scene_SelectionChanged(object sender, EventArgs e)
        {
#if ODYSSEY
            UpdateMoveToSpecificButtons();
#endif


            var selection = new HashSet<object>();

            var fullySelectedRails = new List<Rail>();

            foreach (var item in currentScene.SelectedObjects)
            {
                if (item is Rail rail)
                {
                    bool isFullySelected = true;
                    foreach (var point in rail.PathPoints)
                    {
                        if (point.Selected)
                            selection.Add(point);
                        else
                            isFullySelected = false;
                    }

                    if(isFullySelected)
                        fullySelectedRails.Add(rail);
                }
                else
                    selection.Add(item);
            }

            if (selection.Count > 1)
            {
                CurrentObjectLabel.Text = MultipleSelected;
                string SelectedObjects = "";
                string previousobject = "";
                int multi = 1;

                List<string> selectedobjectnames = new List<string>();
                for (int i = 0; i < selection.Count; i++)
                    selectedobjectnames.Add(selection.ElementAt(i).ToString());

                selectedobjectnames.Sort();
                for (int i = 0; i < selectedobjectnames.Count; i++)
                {
                    string currentobject = selectedobjectnames[i];
                    if (previousobject == currentobject)
                    {
                        SelectedObjects = SelectedObjects.Remove(SelectedObjects.Length - (multi.ToString().Length + 1));
                        multi++;
                        SelectedObjects += $"x{multi}";
                    }
                    else if (multi > 1)
                    {
                        SelectedObjects += ", " + $"\"{currentobject}\"" + ", ";
                        multi = 1;
                    }
                    else
                    {
                        SelectedObjects += $"\"{currentobject}\"" + ", ";
                        multi = 1;
                    }
                    previousobject = currentobject;
                }
                SelectedObjects = multi > 1 ? SelectedObjects + "." : SelectedObjects.Remove(SelectedObjects.Length - 2) + ".";
                SpotlightToolStripStatusLabel.Text = SelectedText + $" {SelectedObjects}";


                if(fullySelectedRails.Count==1 && selection.Count == fullySelectedRails[0].PathPoints.Count)
                {
                    MainTabControl.SelectedTab = ObjectsTabPage;
                    MainSceneListView.TryEnsureVisible(fullySelectedRails[0]);
                }
            }
            else if (selection.Count == 0)
                CurrentObjectLabel.Text = SpotlightToolStripStatusLabel.Text = NothingSelected;
            else
            {
                object selected = selection.First();

                CurrentObjectLabel.Text = selected.ToString() + " " + SelectedText.ToLower();
                SpotlightToolStripStatusLabel.Text = SelectedText+$" \"{selected.ToString()}\".";

                MainTabControl.SelectedTab = ObjectsTabPage;
                MainSceneListView.TryEnsureVisible(selected);
            }
            MainSceneListView.Refresh();

            currentScene.SetupObjectUIControl(ObjectUIControl);
            ObjectUIControl.Refresh();
        }

        private void SplitContainer2_Panel2_Click(object sender, EventArgs e)
        {
            if (currentScene == null || currentScene.SelectedObjects.Count == 0)
                return;
            General3dWorldObject obj = (currentScene.SelectedObjects.First() as General3dWorldObject);
            Rail rail = (currentScene.SelectedObjects.First() as Rail);

            if(Debugger.IsAttached)
                Debugger.Break();
            else
            {
                if (MessageBox.Show("All selected objects will be saved into a gltf file [EXPERIMENTAL]\ncontinue?", "", MessageBoxButtons.YesNo) != DialogResult.Yes)
                    return;
            }



            SharpGLTF.Scenes.SceneBuilder scene = new SharpGLTF.Scenes.SceneBuilder(currentScene.ToString());

            SharpGLTF.Materials.MaterialBuilder material = new SharpGLTF.Materials.MaterialBuilder("Default");

            foreach (General3dWorldObject _obj in currentScene.SelectedObjects.Where(x=>x is General3dWorldObject))
            {
                string mdlName = string.IsNullOrEmpty(_obj.ModelName) ? _obj.ObjectName : _obj.ModelName;

                if (BfresModelRenderer.TryGetModel(mdlName, out BfresModelRenderer.CachedModel cachedModel))
                {
                    scene.AddRigidMesh(cachedModel.VaosToMesh(LevelGLControlModern, material), mdlName,
                        System.Numerics.Matrix4x4.CreateScale(_obj.Scale.X, _obj.Scale.Y, _obj.Scale.Z) *
                        System.Numerics.Matrix4x4.CreateRotationX(_obj.Rotation.X / 180f * Framework.PI) *
                        System.Numerics.Matrix4x4.CreateRotationY(_obj.Rotation.Y / 180f * Framework.PI) *
                        System.Numerics.Matrix4x4.CreateRotationZ(_obj.Rotation.Z / 180f * Framework.PI) *
                        System.Numerics.Matrix4x4.CreateTranslation(_obj.Position.X, _obj.Position.Y, _obj.Position.Z)
                        );
                }
            }

            var model = scene.ToGltf2();

            string fileName = System.IO.Path.Combine(AppContext.BaseDirectory, currentScene.ToString() + ".gltf");

            model.SaveGLTF(fileName);
            MessageBox.Show("Model saved as " + fileName);
        }

        private void Scene_ObjectsMoved(object sender, EventArgs e)
        {
            ObjectUIControl.Refresh();
        }

        #region Toolstrip Items

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog() { Filter =
                $"{FileLevelOpenFilter.Split('|')[0]}|*Stage{SM3DWorldZone.MAP_SUFFIX};*Stage{SM3DWorldZone.COMBINED_SUFFIX};*Island{SM3DWorldZone.COMBINED_SUFFIX}|" +
                $"{FileLevelOpenFilter.Split('|')[0]}|*{SM3DWorldZone.MAP_SUFFIX}|" +
                $"{FileLevelOpenFilter.Split('|')[1]}|*{SM3DWorldZone.DESIGN_SUFFIX}|" +
                $"{FileLevelOpenFilter.Split('|')[2]}|*{SM3DWorldZone.SOUND_SUFFIX}|" +
                $"{FileLevelOpenFilter.Split('|')[3]}|*.szs",
                InitialDirectory = currentScene?.EditZone.Directory ?? (Program.ProjectPath.Equals("") ? Program.BaseStageDataPath : System.IO.Path.Combine(Program.ProjectPath, "StageData")) };

            SpotlightToolStripStatusLabel.Text = StatusWaitMessage;

            if (ofd.ShowDialog() == DialogResult.OK && ofd.FileName != "")
                OpenLevel(ofd.FileName);
            else
                SpotlightToolStripStatusLabel.Text = StatusOpenCancelledMessage;
        }

        private void OpenExToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string stagelistpath = Program.TryGetPathViaProject("SystemData", "StageList.szs");
            if (!File.Exists(stagelistpath))
            {
                MessageBox.Show(string.Format(LevelSelectMissing, stagelistpath), "404", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SpotlightToolStripStatusLabel.Text = StatusOpenFailedMessage;
                return;
            }
            SpotlightToolStripStatusLabel.Text = StatusWaitMessage;
            Refresh();


            LevelSelectForm LPSF;

            if (StageList.TryOpen(stagelistpath, out var STGLST))
                LPSF = new LevelSelectForm(STGLST, true);
            else
            {
                #region load StageList CaptainToad
                SarcData sarc = SARC.UnpackRamN(SZS.YAZ0.Decompress(stagelistpath));

                BymlFileData byml = ByamlFile.LoadN(new MemoryStream(sarc.Files["StageList.byml"]), true, ByteOrder.BigEndian);

                List<List<string>> seasons = new List<List<string>>();

                var regex = new System.Text.RegularExpressions.Regex("Chapter[0-9]_[0-9]");

                foreach (var season in byml.RootNode["SeasonList"])
                {
                    seasons.Add(((List<dynamic>)season["StageList"]).Select(x => (string)x["StageName"]).Where(x=>!regex.IsMatch(x)).ToList());
                }

                LPSF = new LevelSelectForm(seasons);

                #endregion
            }

            LPSF.ShowDialog();
            if (string.IsNullOrEmpty(LPSF.levelname))
            {
                SpotlightToolStripStatusLabel.Text = StatusOpenCancelledMessage;
                return;
            }
            if (TryOpenZoneWithLoadingBar(Program.TryGetPathViaProject("StageData", $"{LPSF.levelname}{SM3DWorldZone.MAP_SUFFIX}"), out var zone))
                OpenZone(zone);
            else if (TryOpenZoneWithLoadingBar(Program.TryGetPathViaProject("StageData", $"{LPSF.levelname}{SM3DWorldZone.COMBINED_SUFFIX}"), out zone))
                OpenZone(zone);
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (currentScene == null)
                return;

            currentScene.Save();
            Scene_IsSavedChanged(null, null);
            SpotlightToolStripStatusLabel.Text = StatusLevelSavedMessage;
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (currentScene == null)
                return;

            if (currentScene.SaveAs())
            {
                Scene_IsSavedChanged(null, null);
                SpotlightToolStripStatusLabel.Text = StatusLevelSavedMessage;
            }
            else
                SpotlightToolStripStatusLabel.Text = StatusSaveCancelledOrFailedMessage;
        }

        private void OptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SettingsForm SF = new SettingsForm(this);
            SF.ShowDialog();
            SpotlightToolStripStatusLabel.Text = StatusSettingsSavedMessage;
        }

        //----------------------------------------------------------------------------

        private void UndoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentScene?.Undo();
            LevelGLControlModern.Refresh();
        }

        private void RedoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentScene?.Redo();
            LevelGLControlModern.Refresh();
        }

        private void AddObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Program.ParameterDB == null)
            {
//                MessageBox.Show(
//@"Y o u  c h o s e  n o t  t o  g e n e r a t e
//a  v a l i d  d a t a b a s e  r e m e m b e r ?
//= )"); //As much as I wish we could keep this, we can't.

                DialogResult DR = MessageBox.Show(DatabaseInvalidText, DatabaseInvalidHeader, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                if (DR == DialogResult.Yes)
                {
                    Thread DatabaseGenThread = new Thread(() =>
                    {
                        LoadLevelForm LLF = new LoadLevelForm("GenDatabase", "GenDatabase");
                        LLF.ShowDialog();
                    });
                    DatabaseGenThread.Start();
                    Program.ParameterDB = new ObjectParameterDatabase();
                    Program.ParameterDB.Create(Program.BaseStageDataPath);
                    Program.ParameterDB.Save(Program.SOPDPath);
                    if (DatabaseGenThread.IsAlive)
                        LoadLevelForm.DoClose = true;
                }
                MessageBox.Show(DatabaseCreatedText, DatabaseCreatedHeader, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var form = new AddObjectForm(currentScene, QuickFavoriteControl);
            form.ShowDialog(this);

            if(form.SomethingWasSelected)
            {
                SpotlightToolStripStatusLabel.Text = StatusObjectPlaceNoticeMessage;

                CancelAddObjectButton.Visible = true;
            }
        }

        private void AddZoneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (currentScene.EditZoneIndex != 0)
                return;

            AddZoneForm azf = new AddZoneForm();

            if (azf.ShowDialog() == DialogResult.Cancel)
                return;

            if (azf.SelectedFileName == null)
                return;

            if (TryOpenZoneWithLoadingBar(System.IO.Path.Combine(Program.BaseStageDataPath,azf.SelectedFileName), out SM3DWorldZone zone))
            {
                var zonePlacement = new ZonePlacement(Vector3.Zero, Vector3.Zero, "Common", zone);
                currentScene.ZonePlacements.Add(zonePlacement);
                currentScene.AddToUndo(new RevertableSingleAddition(zonePlacement, currentScene.ZonePlacements));
                ZoneDocumentTabControl_SelectedTabChanged(null, null);
            }
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentScene?.CopySelectedObjects();
        }

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentScene?.PasteCopiedObjects();
        }

        private void DuplicateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (currentScene.SelectedObjects.Count == 0)
                SpotlightToolStripStatusLabel.Text = DuplicateNothingMessage;
            else
            {
                string SelectedObjects = "";
                string previousobject = "";
                int multi = 1;

                List<string> selectedobjectnames = new List<string>();
                for (int i = 0; i < currentScene.SelectedObjects.Count; i++)
                    selectedobjectnames.Add(currentScene.SelectedObjects.ElementAt(i).ToString());

                selectedobjectnames.Sort();
                for (int i = 0; i < selectedobjectnames.Count; i++)
                {
                    string currentobject = selectedobjectnames[i];
                    if (previousobject == currentobject)
                    {
                        SelectedObjects = SelectedObjects.Remove(SelectedObjects.Length - (multi.ToString().Length + 1));
                        multi++;
                        SelectedObjects += $"x{multi}";
                    }
                    else if (multi > 1)
                    {
                        SelectedObjects += ", " + $"\"{currentobject}\"" + ", ";
                        multi = 1;
                    }
                    else
                    {
                        SelectedObjects += $"\"{currentobject}\"" + ", ";
                        multi = 1;
                    }
                    previousobject = currentobject;
                }
                SelectedObjects = multi > 1 ? SelectedObjects + "." : SelectedObjects.Remove(SelectedObjects.Length - 2) + ".";

                currentScene.DuplicateSelectedObjects();
                currentScene.SetupObjectUIControl(ObjectUIControl);
                MainSceneListView.Refresh();
                ObjectUIControl.Invalidate();

                SpotlightToolStripStatusLabel.Text = string.Format(DuplicateSuccessMessage, SelectedObjects);
            }
        }

        private void DeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (currentScene == null)
                return;

            string DeletedObjects = "";
            for (int i = 0; i < currentScene.SelectedObjects.Count; i++)
                DeletedObjects += currentScene.SelectedObjects.ElementAt(i).ToString() + (i + 1 == currentScene.SelectedObjects.Count ? "." : ", ");
            SpotlightToolStripStatusLabel.Text = string.Format(StatusObjectsDeletedMessage, DeletedObjects);

            currentScene.DeleteSelected();
            LevelGLControlModern.Refresh();
            MainSceneListView.Refresh();
            Scene_SelectionChanged(this, null);
        }

        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (currentScene == null)
                return;

            foreach (I3dWorldObject obj in currentScene.Objects)
                obj.SelectAll(LevelGLControlModern);

            LevelGLControlModern.Refresh();
            MainSceneListView.Refresh();
        }

        private void DeselectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (currentScene == null)
                return;

            foreach (I3dWorldObject obj in currentScene.Objects)
                obj.DeselectAll(LevelGLControlModern);

            LevelGLControlModern.Refresh();
            MainSceneListView.Refresh();
        }

        private void createViewGroupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (currentScene == null)
                return;

            int SelectedObjectCount = 0;
            Vector3 AveragePosition = new Vector3(0,0,0);
            int MaxObjID = 0;
            

            //Get selected objects, and check what the highest viewgroup ID is
            General3dWorldObject CheckedObject;
            List<I3dWorldObject> selectedObjects = new List<I3dWorldObject>();
            foreach (I3dWorldObject obj in currentScene.Objects)
            {
                if (obj is General3dWorldObject)
                {
                    CheckedObject = (General3dWorldObject)obj;
                    if (obj.IsSelectedAll())
                    {
                        

                        selectedObjects.Add(obj);
                        AveragePosition *= (float)SelectedObjectCount;
                        AveragePosition += CheckedObject.Position;
                        SelectedObjectCount++;
                        AveragePosition /= (float)SelectedObjectCount;
                    }

                    if (CheckedObject.ID.StartsWith("vgrp"))
                    {
                        try
                        {
                            int thisID = Convert.ToInt32(CheckedObject.ID.Replace("vgrp",""));
                            if (thisID >= MaxObjID) MaxObjID = thisID + 1;
                        }
                        catch { }
                    }
                }
            }


            //Create GroupView object
            SM3DWorldZone zone = currentScene.EditZone;
            LevelIO.ObjectInfo objectInfo = new LevelIO.ObjectInfo();
            objectInfo.PropertyEntries = new Dictionary<string, ByamlIterator.DictionaryEntry>();
            objectInfo.ID = "vgrp" + MaxObjID.ToString();
            objectInfo.Layer = "Common";
            objectInfo.ClassName = "GroupView";
            objectInfo.ObjectName = "GroupView";
            objectInfo.Scale = new Vector3(1, 1, 1);
            objectInfo.Position = AveragePosition + new Vector3(0,1,0);

            General3dWorldObject ViewGroup = new General3dWorldObject(objectInfo, zone, out bool _LL);

            zone.LinkedObjects.Add(ViewGroup);

            //Create Links
            foreach (I3dWorldObject selObj in selectedObjects)
            {
                if (selObj.Links == null) selObj.Links = new Dictionary<string, List<I3dWorldObject>>();
                if (selObj.Links.ContainsKey("ViewGroup")) selObj.Links["ViewGroup"].Add(ViewGroup);
                else selObj.Links["ViewGroup"] = new List<I3dWorldObject> { ViewGroup };

                ViewGroup.AddLinkDestination("ViewGroup", selObj);
            }

            

        }

        private void GrowSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentScene?.GrowSelection();
        }

        private void SelectAllLinkedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentScene?.SelectAllLinked();
        }

        private void MoveToLinkedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MoveToTargetList(currentScene.EditZone.LinkedObjects);
            SpotlightToolStripStatusLabel.Text = StatusMovedToLinksMessage;
        }

        private void MoveToTargetList(ObjectList targetObjList)
        {
            AdditionManager additionManager = new AdditionManager();
            DeletionManager deletionManager = new DeletionManager();


            foreach (var objList in currentScene.EditZone.ObjLists.Values.Append(currentScene.EditZone.LinkedObjects))
            {
                if (objList == targetObjList)
                    continue;

                var selected = objList.Where(x => x.IsSelectedAll()).ToArray();

                if (selected.Length == 0)
                    continue;

                additionManager.Add(targetObjList, selected);
                deletionManager.Add(objList, selected);
            }
            currentScene.BeginUndoCollection();
            currentScene.ExecuteAddition(additionManager);
            currentScene.ExecuteDeletion(deletionManager);
            currentScene.EndUndoCollection();
        }

        private void MoveToAppropriateListsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AdditionManager additionManager = new AdditionManager();
            DeletionManager deletionManager = new DeletionManager();

            var linkedObjs = currentScene.EditZone.LinkedObjects;

            foreach (var objList in currentScene.EditZone.ObjLists.Values)
            {
                var selected = objList.Where(x => x.IsSelectedAll()).ToArray();

                if (selected.Length == 0)
                    continue;

                for (int i = 0; i < selected.Length; i++)
                {
                    if(!selected[i].TryGetObjectList(currentScene.EditZone, out ObjectList _objList))
                        _objList = linkedObjs;


                    if (objList != _objList)
                    {
                        additionManager.Add(_objList, selected[i]);
                        deletionManager.Add(objList, selected[i]);
                    }
                }
            }

            {
                var objList = linkedObjs;

                var selected = objList.Where(x => x.IsSelectedAll()).ToArray();

                if (selected.Length != 0)
                {
                    for (int i = 0; i < selected.Length; i++)
                    {
                        if (!selected[i].TryGetObjectList(currentScene.EditZone, out ObjectList _objList))
                            _objList = linkedObjs;


                        if (objList != _objList)
                        {
                            additionManager.Add(_objList, selected[i]);
                            deletionManager.Add(objList, selected[i]);
                        }
                    }
                }
            }

            currentScene.BeginUndoCollection();
            currentScene.ExecuteAddition(additionManager);
            currentScene.ExecuteDeletion(deletionManager);
            currentScene.EndUndoCollection();
            SpotlightToolStripStatusLabel.Text = StatusMovedFromLinksMessage;
        }

        private void LevelParametersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string stagelistpath = Program.TryGetPathViaProject("SystemData", "StageList.szs");
            if (!File.Exists(stagelistpath))
            {
                MessageBox.Show(string.Format(LevelSelectMissing, stagelistpath), "404", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            SpotlightToolStripStatusLabel.Text = StatusWaitMessage;
            Refresh();

            if(!StageList.TryOpen(stagelistpath, out var STGLST))
            {
                MessageBox.Show(InvalidStageListFormatText, InvalidStageListFormatHeader, MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            LPF = new LevelParameterForm(STGLST, currentScene?.ToString() ?? "");
            LPF.Show();
        }

        //----------------------------------------------------------------------------

        private void EditObjectsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (currentScene.GetType() != typeof(SM3DWorldScene))
            {
                currentScene = currentScene.ConvertToOtherSceneType<SM3DWorldScene>();
                AssignSceneEvents(currentScene);
                LevelGLControlModern.MainDrawable = currentScene;
                LevelGLControlModern.Refresh();
            }
        }

        private void EditLinksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (currentScene.GetType() != typeof(LinkEdit3DWScene))
            {
                var linkEditScene = currentScene.ConvertToOtherSceneType<LinkEdit3DWScene>();
                linkEditScene.DestinationChanged += LinkEditScene_DestinationChanged;
                currentScene = linkEditScene;
                AssignSceneEvents(currentScene);
                LevelGLControlModern.MainDrawable = currentScene;
                LevelGLControlModern.Refresh();
            }
        }

        private void LinkEditScene_DestinationChanged(object sender, DestinationChangedEventArgs e)
        {
            SpotlightToolStripStatusLabel.Text = e.DestWasMovedToLinked ? (string.Format(MovedToOtherListInfo, e.LinkDestination, "Common_Linked")) : NothingMovedToLinkedInfo;
        }


        //----------------------------------------------------------------------------

        private void SpotlightWikiToolStripMenuItem_Click(object sender, EventArgs e) => Process.Start("https://github.com/jupahe64/Spotlight/wiki");

        private void CheckForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CheckForUpdates(out Version Latest, true))
            {
                if (MessageBox.Show(string.Format(UpdateReadyText, Latest.ToString()), UpdateReadyHeader, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    Process.Start("https://github.com/jupahe64/Spotlight/releases");
            }
            else if (!Latest.Equals(new Version(0, 0, 0, 0)))
                MessageBox.Show(string.Format(UpdateNoneText, new Version(Application.ProductVersion).ToString()), UpdateNoneHeader, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion
        
        private void OpenLevel(string Filename)
        {
            if (TryOpenZoneWithLoadingBar(Filename, out var zone))
                OpenZone(zone);
        }

        public bool TryOpenZoneWithLoadingBar(string fileName, out SM3DWorldZone zone)
        {
            if (SM3DWorldZone.TryGetLoadedZone(fileName, out zone))
            {
                return true;
            }
            else if (SM3DWorldZone.TryGetLoadingInfo(fileName, out var loadingInfo))
            {
                Thread LoadingThread = new Thread((n) =>
                {
                    LoadLevelForm LLF = new LoadLevelForm(n.ToString(), "LoadLevel");
                    LLF.ShowDialog();
                });
                LoadingThread.Start(loadingInfo?.levelName);

                zone = new SM3DWorldZone(loadingInfo.Value);

                if (LoadingThread.IsAlive)
                    LoadLevelForm.DoClose = true;

                SpotlightToolStripStatusLabel.Text = string.Format(StatusOpenSuccessMessage, loadingInfo?.levelName);

                SetAppStatus(true);

                return true;
            }
            else
            {
                zone = null;
                return false;
            }
        }

        public void OpenZone(SM3DWorldZone zone)
        {
            SM3DWorldScene scene = new SM3DWorldScene(zone);

            AssignSceneEvents(scene);

            scene.EditZoneIndex = 0;

            //indirectly calls ZoneDocumentTabControl_SelectedTabChanged
            //which already sets up a lot
            ZoneDocumentTabControl.AddTab(new DocumentTabControl.DocumentTab(zone.LevelName, scene), true);

            Scene_IsSavedChanged(null, null);

            #region focus on player if it exists
            const string playerListName = SM3DWorldZone.MAP_PREFIX + "PlayerList";

            if (zone.ObjLists.ContainsKey(playerListName) && zone.ObjLists[playerListName].Count > 0)
            {
                scene.GL_Control.CamRotX = 0;
                scene.GL_Control.CamRotY = HALF_PI / 4;
                scene.FocusOn(zone.ObjLists[playerListName][0]);
            }
            #endregion

            MainTabControl.SelectedTab = ObjectsTabPage;
        }

        private void AssignSceneEvents(SM3DWorldScene scene)
        {
            scene.SelectionChanged += Scene_SelectionChanged;
            scene.ListChanged += Scene_ListChanged;
            scene.ListEntered += Scene_ListEntered;
            scene.ObjectsMoved += Scene_ObjectsMoved;
            scene.ZonePlacementsChanged += Scene_ZonePlacementsChanged;
            scene.Reverted += Scene_Reverted;
            scene.IsSavedChanged += Scene_IsSavedChanged;
            scene.ObjectPlaced += Scene_ObjectPlaced;
        }

        private void Scene_IsSavedChanged(object sender, EventArgs e)
        {
            foreach (var tab in ZoneDocumentTabControl.Tabs)
            {
                SM3DWorldScene scene = (SM3DWorldScene)tab.Document;

                tab.Name = ((!string.IsNullOrEmpty(Program.ProjectPath) && scene.MainZone.Directory==Program.BaseStageDataPath) ? "[GamePath]" : string.Empty) +
                    scene.ToString() + 
                    (scene.IsSaved ? string.Empty : "*");
            }

            ZoneDocumentTabControl.Invalidate();
        }

        private void Scene_Reverted(object sender, RevertedEventArgs e)
        {
            UpdateZoneList();
            MainSceneListView.Refresh();
            currentScene.SetupObjectUIControl(ObjectUIControl);
        }

        private void Scene_ZonePlacementsChanged(object sender, EventArgs e)
        {
            UpdateZoneList();
        }

        private void Scene_ListEntered(object sender, ListEventArgs e)
        {
            if(e.List is List<RailPoint> points)
            {
                if (points.Count > 0)
                {
                    MainTabControl.SelectedTab = ObjectsTabPage;
                    MainSceneListView.TryEnsureVisible(points[0]);
                }

                return;
            }

            MainSceneListView.EnterList(e.List);

            MainTabControl.SelectedTab = ObjectsTabPage;
        }

        private void MainSceneListView_ItemClicked(object sender, ItemClickedEventArgs e)
        {
            if (e.Clicks == 2 && e.Item is I3dWorldObject obj)
                currentScene.FocusOn(obj);
            else if(e.Item is ZonePlacement placement)
            {
                if (e.Clicks == 2)
                    LevelGLControlModern.CameraTarget = placement.Position;
                else if (e.Clicks == 3)
                {
                    int index = 0;

                    foreach (var zone in currentScene.GetZones())
                    {
                        if(placement.Zone == zone)
                        {
                            ZoneListBox.SelectedIndex = index;
                            break;
                        }
                        index++;
                    }
                }

            }
        }

        private void EditIndividualButton_Click(object sender, EventArgs e)
        {
            OpenZone(currentScene.EditZone);
        }

        private void ZoneDocumentTabControl_SelectedTabChanged(object sender, EventArgs e)
        {
            if (ZoneDocumentTabControl.SelectedTab == null)
            {
                return;
            }

            currentScene = (SM3DWorldScene)ZoneDocumentTabControl.SelectedTab.Document;

            MainSceneListView.Scene = currentScene;

            #region setup UI
            ObjectUIControl.ClearObjectUIContainers();
            ObjectUIControl.Refresh();

            MainSceneListView.Refresh();

            UpdateZoneList();

            LevelGLControlModern.MainDrawable = currentScene;
            #endregion
        }

        private void UpdateZoneList()
        {
            ZoneListBox.BeginUpdate();
            ZoneListBox.Items.Clear();

            foreach (var item in currentScene.GetZones())
            {
                ZoneListBox.Items.Add(item);
            }

            ZoneListBox.SelectedIndex = currentScene.EditZoneIndex;

            ZoneListBox.EndUpdate();
        }

        private void ZoneDocumentTabControl_TabClosing(object sender, DocumentTabClosingEventArgs e)
        {
            SM3DWorldScene scene = (SM3DWorldScene)e.Tab.Document;

            #region find out which zones will remain after the zones in this scene gets unloaded
            HashSet<SM3DWorldZone> remainingZones = new HashSet<SM3DWorldZone>();

            foreach(var tab in ZoneDocumentTabControl.Tabs)
            {
                if (tab == e.Tab)
                    continue; //wont remain

                foreach (var zone in ((SM3DWorldScene)tab.Document).GetZones())
                    remainingZones.Add(zone);
            }
            #endregion

            #region find out which zones need to get unloaded
            HashSet<SM3DWorldZone> unsavedZones = new HashSet<SM3DWorldZone>();

            HashSet<SM3DWorldZone> zonesToUnload = new HashSet<SM3DWorldZone>();

            foreach (var zone in scene.GetZones())
            {
                if (!remainingZones.Contains(zone))
                {
                    zonesToUnload.Add(zone);

                    if (!zone.IsSaved && !remainingZones.Contains(zone))
                        unsavedZones.Add(zone);
                }
            }
            #endregion

            #region ask to save unsaved changes
            if (unsavedZones.Count>0)
            {
                string UnsavedZones = "";

                foreach (var zone in unsavedZones)
                    UnsavedZones += zone.LevelFileName+"\n";

                switch (MessageBox.Show(string.Format(UnsavedChangesText, UnsavedZones), UnsavedChangesHeader, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning))
                {
                    case DialogResult.Yes:
                        foreach (var zone in unsavedZones)
                            zone.Save();

                        Scene_IsSavedChanged(null, null);
                        break;

                    case DialogResult.No:
                        break;

                    case DialogResult.Cancel:
                        e.Cancel = true;
                        return;
                }
            }
            #endregion

            //unload all zones that need to be unloaded
            foreach (var zone in zonesToUnload)
                zone.Unload();


            #region disable all controls if this is the last tab
            if (ZoneDocumentTabControl.Tabs.Count == 1)
            {
                ZoneListBox.Items.Clear();
                MainSceneListView.UnselectCurrentList();
                ObjectUIControl.ClearObjectUIContainers();
                ObjectUIControl.Refresh();

                SetAppStatus(false);
                string levelname = currentScene.EditZone.LevelName;
                currentScene = null;
                LevelGLControlModern.MainDrawable = null;
                SpotlightToolStripStatusLabel.Text = string.Format(StatusLevelClosed, levelname);
            }
            #endregion
        }

        private void ZoneListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            currentScene.EditZoneIndex = ZoneListBox.SelectedIndex;

            MainSceneListView.CollapseAllNodes();

            if (currentScene.EditZone.HasCategoryMap)
                MainSceneListView.ExpandNode(SM3DWorldZone.MAP_PREFIX);
            else if (currentScene.EditZone.HasCategoryDesign)
                MainSceneListView.ExpandNode(SM3DWorldZone.DESIGN_PREFIX);
            else
                MainSceneListView.ExpandNode(SM3DWorldZone.SOUND_PREFIX);


            MainSceneListView.Refresh();

            UpdateMoveToSpecificButtons();

            EditIndividualButton.Enabled = ZoneListBox.SelectedIndex > 0;

            currentScene.SetupObjectUIControl(ObjectUIControl);
            LevelGLControlModern.Refresh();
        }

        private void SceneListView3dWorld1_SelectionChanged(object sender, EventArgs e)
        {
            LevelGLControlModern.Refresh();

            Scene_SelectionChanged(this, null);
        }

        private void UpdateMoveToSpecificButtons()
        {
            List<ToolStripItem> items = new List<ToolStripItem>();

            using (Graphics cg = CreateGraphics())
            {
                foreach (var (listName, objList) in currentScene.EditZone.ObjLists)
                {
                    var btn = new ToolStripButton(listName, null, MoveToListMenuItem_Click);

                    btn.Width = (int)Math.Ceiling(cg.MeasureString(listName, btn.Font).Width);

                    items.Add(btn);
                }
            }

            MoveToSpecificListToolStripMenuItem.DropDownItems.Clear();
            MoveToSpecificListToolStripMenuItem.DropDownItems.AddRange(items.ToArray());
        }

        /// <summary>
        /// Sets the status of the application. This changes the Enabled property of controls that need it.
        /// </summary>
        /// <param name="Trigger">The state to set the Enabled property to</param>
        private void SetAppStatus(bool Trigger)
        {
            SaveToolStripMenuItem.Enabled = Trigger;
            SaveAsToolStripMenuItem.Enabled = Trigger;
            UndoToolStripMenuItem.Enabled = Trigger;
            RedoToolStripMenuItem.Enabled = Trigger;
            AddZoneToolStripMenuItem.Enabled = Trigger;
            CopyToolStripMenuItem.Enabled = Trigger;
            PasteToolStripMenuItem.Enabled = Trigger;
            DuplicateToolStripMenuItem.Enabled = Trigger;
            DeleteToolStripMenuItem.Enabled = Trigger;
            MoveSelectionToToolStripMenuItem.Enabled = Trigger;
            SelectionToolStripMenuItem.Enabled = Trigger;
            ModeToolStripMenuItem.Enabled = Trigger;
            MainSceneListView.Enabled = Trigger;
        }

        private void LevelEditorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = !ZoneDocumentTabControl.TryClearTabs();
        }

        private void Scene_ObjectPlaced(object sender, EventArgs e)
        {
            MainSceneListView.Refresh();
            if (currentScene.ObjectPlaceDelegate == null)
            {
                SpotlightToolStripStatusLabel.Text = "";
                QuickFavoriteControl.Deselect();
                CancelAddObjectButton.Visible = false;
            }
        }

        /// <summary>
        /// Checks for Updates
        /// </summary>
        /// <returns></returns>
        public bool CheckForUpdates(out Version Latest, bool ShowError = false)
        {
            System.Net.WebClient Client = new System.Net.WebClient();
            Latest = null;
            try
            {
                Client.DownloadFile("https://raw.githubusercontent.com/jupahe64/Spotlight/master/SpotLight/LatestVersion.txt", @AppDomain.CurrentDomain.BaseDirectory + "VersionCheck.txt");
            }
            catch (Exception e)
            {
                if (ShowError)
                    MessageBox.Show(string.Format(UpdateFailText, e.Message), UpdateFailHeader, MessageBoxButtons.OK, MessageBoxIcon.Warning); //Imagine not having internet
                Latest = new Version(0, 0, 0, 0);
                return false;
            }
            if (File.Exists(@AppDomain.CurrentDomain.BaseDirectory + "VersionCheck.txt"))
            {
                Version Internet = new Version(File.ReadAllText(@AppDomain.CurrentDomain.BaseDirectory + "VersionCheck.txt"));
                File.Delete(@AppDomain.CurrentDomain.BaseDirectory + "VersionCheck.txt");
                Version Local = new Version(Application.ProductVersion);
                if (Local.CompareTo(Internet) < 0)
                {
                    Latest = Internet;
                    return true;
                }
                else
                {
                    Latest = Local;
                    return false;
                }
            }
            else return false;
        }

        #region Translations
        /// <summary>
        /// Sets up the text. if this is not called, I bet there will be some crashes
        /// </summary>
        public void Localize()
        {
            Text = Program.CurrentLanguage.GetTranslation("EditorTitle") ?? "Spotlight";


            #region Controls
            #region Toolstrip Items
            this.Localize(
            FileToolStripMenuItem,
            OpenToolStripMenuItem,
            OpenExToolStripMenuItem,
            SaveToolStripMenuItem,
            SaveAsToolStripMenuItem,
            OptionsToolStripMenuItem,

            EditToolStripMenuItem,
            UndoToolStripMenuItem,
            RedoToolStripMenuItem,
            AddObjectToolStripMenuItem,
            AddZoneToolStripMenuItem,
            DuplicateToolStripMenuItem,
            DeleteToolStripMenuItem,
            LevelParametersToolStripMenuItem,
            MoveSelectionToToolStripMenuItem,
            MoveToLinkedToolStripMenuItem,
            MoveToAppropriateListsToolStripMenuItem,

            SelectionToolStripMenuItem,
            SelectAllToolStripMenuItem,
            DeselectAllToolStripMenuItem,
            GrowSelectionToolStripMenuItem,
            SelectAllLinkedToolStripMenuItem,

            ModeToolStripMenuItem,
            EditObjectsToolStripMenuItem,
            EditLinksToolStripMenuItem,

            AboutToolStripMenuItem,
            SpotlightWikiToolStripMenuItem,
            CheckForUpdatesToolStripMenuItem,
            #endregion

            EditIndividualButton,
            CancelAddObjectButton
            );
            CurrentObjectLabel.Text = NothingSelected;
            #endregion
        }

        [Program.Localized]
        string WelcomeMessageHeader = "Introduction";
        [Program.Localized]
        string WelcomeMessageText =
@"Welcome to Spotlight!

In order to use this program, you will need the folders ""StageData"" and ""ObjectData"" from Super Mario 3D World

Please select the folder than contains these folders";
        [Program.Localized]
        string StatusWelcomeMessage = "Welcome to Spotlight!";
        [Program.Localized]
        string StatusWelcomeBackMessage = "Welcome back!";
        [Program.Localized]
        string DatabasePickerTitle = "Select the Game Directory of Super Mario 3D World";
        [Program.Localized]
        string InvalidGamepathText = "The Directory doesn't contain ObjectData and StageData.";
        [Program.Localized]
        string InvalidGamepathHeader = "The GamePath is invalid";
        [Program.Localized]
        string DatabaseMissingText =
@"Spotlight could not find the Object Parameter Database (ParameterDatabase.sopd)

Spotlight needs an Object Parameter Database in order for you to add objects.

Would you like to generate a new object Database from your 3DW Directory?";
        [Program.Localized]
        string DatabaseMissingHeader = "Database Missing";
        [Program.Localized]
        string DatabaseExcludeText = "Are you sure? You cannot add objects without it.";
        [Program.Localized]
        string DatabaseExcludeHeader = "Confirmation";
        [Program.Localized]
        string DatabaseOutdatedText =
                @"The Loaded Database is outdated ({0}).
The latest Database version is {1}.
Would you like to rebuild the database from your 3DW Files?";
        [Program.Localized]
        string DatabaseOutdatedHeader = "Database Outdated";

        [Program.Localized]
        string StatusOpenSuccessMessage = "{0} has been Loaded successfully.";
        [Program.Localized]
        string StatusWaitMessage = "Waiting...";
        [Program.Localized]
        string StatusOpenCancelledMessage = "Open Cancelled";
        [Program.Localized]
        string StatusOpenFailedMessage = "Open Failed";
        [Program.Localized]
        string StatusLevelSavedMessage = "Level Saved!";
        [Program.Localized]
        string StatusSettingsSavedMessage = "Settings Saved.";
        [Program.Localized]
        string StatusSaveCancelledOrFailedMessage = "Save Cancelled or Failed.";
        [Program.Localized]
        string StatusObjectPlaceNoticeMessage = "You have to place the object by clicking, when holding shift multiple objects can be placed";
        [Program.Localized]
        string FileLevelOpenFilter = "Level Files (Map)|Level Files (Design)|Level Files (Sound)|All Level Files";
        
        [Program.Localized]
        string DuplicateNothingMessage = "Can't duplicate nothing!";
        [Program.Localized]
        string DuplicateSuccessMessage = "Duplicated {0}";

        [Program.Localized]
        string LevelSelectMissing = "StageList.szs Could not be found inside {0}, so this feature cannot be used.";

        [Program.Localized]
        string StatusObjectsDeletedMessage = "Deleted {0}";
        [Program.Localized]
        string DatabaseInvalidText = "The Database is invalid, and you cannot add objects without one. Would you like to generate one from your SM3DW Files?";
        [Program.Localized]
        string DatabaseInvalidHeader = "Invalid Database";
        [Program.Localized]
        string DatabaseCreatedText = "Database Created";
        [Program.Localized]
        string DatabaseCreatedHeader = "Operation Complete";

        [Program.Localized]
        string StatusMovedToLinksMessage = "Moved to the Link List";
        [Program.Localized]
        string StatusMovedFromLinksMessage = "Moved to the Appropriate List";
        [Program.Localized]
        string UpdateReadyText = "Spotlight Version {0} is currently available for download! Would you like to visit the Download Page?";
        [Program.Localized]
        string UpdateReadyHeader = "Update Ready!";
        [Program.Localized]
        string UpdateNoneText = "You are using the Latest version of Spotlight ({0})";
        [Program.Localized]
        string UpdateNoneHeader = "No Updates";
        [Program.Localized]
        string UpdateFailText = "Failed to retrieve update information.\n{0}";
        [Program.Localized]
        string UpdateFailHeader = "Connection Failure";

        [Program.Localized]
        string UnsavedChangesText = "You have unsaved changes in:\n {0} Do you want to save them?";
        [Program.Localized]
        string UnsavedChangesHeader = "Unsaved Changes!";
        [Program.Localized]
        string StatusLevelClosed = "{0} was closed.";

        [Program.Localized]
        string MultipleSelected = "Multiple Objects Selected";
        [Program.Localized]
        string NothingSelected = "Nothing Selected";
        [Program.Localized]
        string SelectedText = "Selected";

        [Program.Localized]
        string MovedToOtherListInfo = "{0} was moved to {1}";
        [Program.Localized]
        string NothingMovedToLinkedInfo = "All objects stayed in their object list (you held down Shift)";

        [Program.Localized]
        string InvalidStageListFormatText = "The format of the StageList.szs in GamePath does not have editing support yet";
        [Program.Localized]
        string InvalidStageListFormatHeader = "Invalid Format";

        #endregion

        private void QuickFavoriteControl_SelectedFavoriteChanged(object sender, EventArgs e)
        {
            if(currentScene!=null && QuickFavoriteControl.SelectedFavorite!=null)
            {
                currentScene.ObjectPlaceDelegate = QuickFavoriteControl.SelectedFavorite.PlacementHandler;
                SpotlightToolStripStatusLabel.Text = StatusObjectPlaceNoticeMessage;
                CancelAddObjectButton.Visible = true;
            }
            else
            {
                currentScene?.ResetObjectPlaceDelegate();
                SpotlightToolStripStatusLabel.Text = "";
                CancelAddObjectButton.Visible = false;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            LevelGLControlModern_Load(null, null);
        }

        private void LevelGLControlModern_Load(object sender, EventArgs e)
        {
            BfresModelRenderer.Initialize();
            ExtraModelRenderer.Initialize();
            GizmoRenderer.Initialize();
            AreaRenderer.Initialize();
        }

        private void CancelAddObjectButton_Click(object sender, EventArgs e)
        {
            if (currentScene?.ObjectPlaceDelegate != null)
            {
                currentScene.ResetObjectPlaceDelegate();

                QuickFavoriteControl.Deselect();

                SpotlightToolStripStatusLabel.Text = "";
            }

            CancelAddObjectButton.Visible = false;
        }

        private void LevelEditorForm_Shown(object sender, EventArgs e)
        {
            Program.IsProgramReady = true;
            BringToFront();
            Focus();
        }

        private void InvertSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentScene?.InvertSelection();
        }

        private void MoveToListMenuItem_Click(object sender, EventArgs e)
        {
            string listName = ((ToolStripButton)sender).Text;

            //TODO Localize
            if(MessageBox.Show("Are you sure you want to move all selected objects to "+listName+"?", "Confirm", MessageBoxButtons.YesNoCancel) == DialogResult.Yes)
            {
                MoveToTargetList(currentScene.EditZone.ObjLists[listName]);
                //TODO set status text
            }
        }

        private void RestartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tabs = ZoneDocumentTabControl.Tabs.ToArray();
            var selected = ZoneDocumentTabControl.SelectedTab;

            ZoneDocumentTabControl.ClearTabs();

            currentScene?.Disconnect(LevelGLControlModern);

            ((List<GL_EditorFramework.GL_Core.GL_ControlModern>)typeof(Framework).GetField("modernGlControls", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Remove(LevelGLControlModern);

            Program.SetRestartForm(new LevelEditorForm(tabs, selected, QuickFavoriteControl.Favorites.ToArray()));
            Close();
        }

        
    }
}
