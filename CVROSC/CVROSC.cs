using ABI.CCK.Scripts;
using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using Bespoke.Osc;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CVROSC
{
    public sealed class CVROSCMod : MelonMod
    {
        [DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);

        static VRLayer OSCServer = null;

        // Bases
        const string AvatarParamBase = "/avatar/parameters";

        // OSC config
        static bool Wait = true;
        static Guid LocalLowFolder = new Guid("A520A1A4-1780-4FF6-BD18-167343C5AF16");
        static string SavedAvatarGUID = "00000000-0000-0000-0000-000000000000";
        static ViewManager MenuInstance = null;
        static CVRAnimatorManager AnimatorManager = null;
        static List<CVRAdvancedSettingsFileProfileValue> Parameters = null;

        public override void OnUpdate()
        {
            if (OSCServer == null)
            {
                OSCServer = new VRLayer();
                OSCServer.Init(new EventHandler<OscMessageReceivedEventArgs>(MessageF), new EventHandler<OscBundleReceivedEventArgs>(BundleF));

                MelonLogger.Msg("Started Open Sound Control server. Receiving on {0} and sending on {1}.", OSCServer.VRServer.Port, OSCServer.VRClient.Port);
            }

            try
            {
                string CurrentAvatarGUID = MetaPort.Instance.currentAvatarGuid;

                if (!SavedAvatarGUID.Equals(CurrentAvatarGUID) || AnimatorManager == null)
                {
                    SavedAvatarGUID = CurrentAvatarGUID;

                    Wait = true;

                    if (!SavedAvatarGUID.Equals(CurrentAvatarGUID) && AnimatorManager == null)
                    {
                        MelonLogger.Msg(String.Format("Avatar change detected, loading animation manager... ({0})", CurrentAvatarGUID));
                        MelonLogger.Msg(String.Format("OLD: {0}, NEW: {1}", SavedAvatarGUID, CurrentAvatarGUID));
                    }

                    if (PlayerSetup.Instance.animatorManager == null)
                        // Wait for the game to initialize the animator manager
                        return;

                    if (AnimatorManager == null)
                        AnimatorManager = PlayerSetup.Instance.animatorManager;

                    if (MenuInstance == null)
                        MenuInstance = ViewManager.Instance;

                    Parameters = AnimatorManager.GetAdditionalSettingsCurrent();

                    MelonLogger.Msg(String.Format("Animation manager found and cached for {0}, {1} parameters found!", SavedAvatarGUID, Parameters.Count));

                    OSCConfig Config = new OSCConfig();

                    Config.AvatarParameters = new List<OSCParameter>();
                    Config.AvatarName = "unknown";
                    Config.AvatarGUID = SavedAvatarGUID;

                    if (Parameters.Count > 0)
                    {
                        MelonLogger.Msg(String.Format("Scanning parameters for {0}...", Config.AvatarGUID));

                        for (int i = 0; i < Parameters.Count; i++)
                        {
                            OSCParameter AP = null;

                            switch (Parameters[i].value)
                            {
                                case float f:
                                    AP = new OSCParameter();
                                    AP.Input = new OSCAddress();
                                    AP.Output = new OSCAddress();

                                    AP.ParameterName = Parameters[i].name;

                                    AP.Input.ParameterAddress = AP.Output.ParameterAddress = String.Format("{0}/{1}", AvatarParamBase, Parameters[i].name);
                                    AP.Input.ParameterType = AP.Output.ParameterType = "Float";

                                    break;

                                    //case bool b:
                                    //    AP.Input.ParameterType = AP.Output.ParameterType = "Bool";
                                    //    break;

                                    //case int i:
                                    //    AP.Input.ParameterType = AP.Output.ParameterType = "Int";
                                    //    break;
                            }

                            if (AP != null)
                            {
                                Config.AvatarParameters.Add(AP);
                                MelonLogger.Msg(String.Format("Parameter {0} ({1}): {2}", AP.Input.ParameterAddress, AP.Input.ParameterType, Parameters[i].value));
                            }
                        }

                        try
                        {
                            string TargetDir = String.Format("{0}\\Alpha Blend Interactive\\ChilloutVR\\OSC\\usr_{1}\\Avatars\\", GetKnownFolderPath(LocalLowFolder), MetaPort.Instance.ownerId);
                            if (!System.IO.Directory.Exists(TargetDir))
                                System.IO.Directory.CreateDirectory(TargetDir);

                            String AvatarPath = String.Format("{0}\\avtr_{1}.json", TargetDir, Config.AvatarGUID);
                            String JSONData = JsonConvert.SerializeObject(Config, Formatting.Indented);

                            System.IO.File.WriteAllText(AvatarPath, JSONData);

                            MelonLogger.Msg(String.Format("JSON written to \"{0}\"", AvatarPath));
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Error(ex.ToString());
                        }

                        MelonLogger.Msg("The new animation manager is now ready to be controlled through Open Sound Control.");
                    }

                    OSCServer.SendMsg("/avatar/change", OSCServer.VRClient, Config.AvatarGUID, true);

                    Wait = false;
                }
            }
            catch
            {
                MenuInstance = null;
                AnimatorManager = null;
                Parameters = null;

                Wait = true;
            }

            Task.Run(() => GetParameters());
        }

        static void GetParameters()
        {
            if (Wait)
                return;

            try
            {
                if (AnimatorManager != null && Parameters != null)
                {
                    for (int j = 0; j < Parameters.Count; j++)
                    {
                        float? f = AnimatorManager.GetAnimatorParameterFloat(Parameters[j].name);
                        if (f != null)
                        {
                            if (f != Parameters[j].value)
                            {
                                Parameters[j].value = (float)f;
                                OSCServer.SendMsg(String.Format("{0}/{1}", AvatarParamBase, Parameters[j].name), OSCServer.VRClient, Parameters[j].value, true);
                            }
                        }

                        int? i = AnimatorManager.GetAnimatorParameterInt(Parameters[j].name);
                        if (i != null)
                        {
                            if (i != (int)Parameters[j].value)
                            {
                                Parameters[j].value = (float)i;
                                OSCServer.SendMsg(String.Format("{0}/{1}", AvatarParamBase, Parameters[j].name), OSCServer.VRClient, Parameters[j].value, true);
                            }
                        }

                        bool? b = AnimatorManager.GetAnimatorParameterBool(Parameters[j].name);
                        if (b != null)
                        {
                            if (b != (Parameters[j].value == 1f))
                            {
                                Parameters[j].value = Convert.ToSingle(b);
                                OSCServer.SendMsg(String.Format("{0}/{1}", AvatarParamBase, Parameters[j].name), OSCServer.VRClient, Parameters[j].value, true);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex.ToString());

                MenuInstance = null;
                AnimatorManager = null;
                Parameters = null;

                System.Threading.Thread.Sleep(1000);
            }
        }

        static void SetParameter(string Address, object Data)
        {
            switch (Data)
            {
                case bool b:
                case int i:
                case float f:
                    if (Address.StartsWith(AvatarParamBase))
                    {
                        string Variable = Address.Replace(AvatarParamBase, "");
                        // MelonLogger.Msg("Received message {0} with data {1}! ({2})", Variable, Data[0], Address);

                        for (int j = 0; j < Parameters.Count; j++)
                        {
                            if (Parameters[j].name.Equals(Variable))
                            {
                                Parameters[j].value = Convert.ToSingle(Data);
                                AnimatorManager.SetAnimatorParameter(Parameters[j].name, Parameters[j].value);
                                // MenuInstance.gameMenuView.View.TriggerEvent("CVRAppActionLoadAvatarSettings");
                                return;
                            }
                        }
                    }

                    break;

                default:
                    MelonLogger.Error("Received unsupported message at address {0} of type {1}, with value {2}.",
                        Address, Data.GetType().Name, Data);
                    return;
            }
        }

        static void AnalyzeData(object sender, IPEndPoint Source, string Address, object Data)
        {
            if (sender == null) return;

            switch (Data)
            {
                case OscBundle OSCB:
                    foreach (OscMessage bOSCM in OSCB.Messages)
                        SetParameter(Address, bOSCM.Data[0]);

                    foreach (OscBundle bOSCB in OSCB.Bundles)
                        AnalyzeData(sender, Source, Address, bOSCB);

                    break;

                case OscMessage OSCM:
                    SetParameter(Address, OSCM.Data[0]);
                    break;

                default:
                    MelonLogger.Error("Received unsupported packet at address {0}!", Address);
                    return;
            }       
        }

        static void BundleF(object sender, OscBundleReceivedEventArgs Var)
        {
            AnalyzeData(sender, Var.Bundle.SourceEndPoint, Var.Bundle.Address, Var.Bundle);
        }

        static void MessageF(object sender, OscMessageReceivedEventArgs Var)
        {
            AnalyzeData(sender, Var.Message.SourceEndPoint, Var.Message.Address, Var.Message);
        }

        static string GetKnownFolderPath(Guid KnownFolderID)
        {
            IntPtr pszPath = IntPtr.Zero;
            try
            {
                int HResult = SHGetKnownFolderPath(KnownFolderID, 0, IntPtr.Zero, out pszPath);

                if (HResult >= 0)
                    return Marshal.PtrToStringAuto(pszPath);

                throw Marshal.GetExceptionForHR(HResult);
            }
            finally
            {
                if (pszPath != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pszPath);
            }
        }
    }
}