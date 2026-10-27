using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AudioSwitch
{
    internal sealed class AudioDevice
    {
        public string Id;
        public string Name;
        // Nombre del adaptador físico ("USB Audio", "Realtek(R) Audio"); sirve para emparejar salida y micrófono.
        public string InterfaceName;
        public EDataFlow Flow;
    }

    internal sealed class AudioService
    {
        const int DeviceStateActive = 1;
        const int ClsCtxAll = 23;
        const int StgmRead = 0;
        const ushort VtLpwstr = 31;

        static readonly Guid AudioEndpointVolumeId = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        static readonly PropertyKey DeviceFriendlyName = new PropertyKey(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
        static readonly PropertyKey DeviceInterfaceFriendlyName = new PropertyKey(new Guid("026E516E-B814-414B-83CD-856D6FEF4822"), 2);

        readonly IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        readonly Dictionary<string, AudioDevice> deviceCache = new Dictionary<string, AudioDevice>();
        readonly Dictionary<string, IAudioEndpointVolume> volumeCache = new Dictionary<string, IAudioEndpointVolume>();

        public List<AudioDevice> GetDevices(EDataFlow flow)
        {
            List<AudioDevice> devices = new List<AudioDevice>();
            IMMDeviceCollection collection;
            if (enumerator.EnumAudioEndpoints(flow, DeviceStateActive, out collection) != 0 || collection == null)
                return devices;
            try
            {
                int count;
                collection.GetCount(out count);
                for (int i = 0; i < count; i++)
                {
                    IMMDevice device;
                    if (collection.Item(i, out device) != 0 || device == null) continue;
                    try
                    {
                        AudioDevice info = Describe(device, flow);
                        if (info != null) devices.Add(info);
                    }
                    finally { Marshal.ReleaseComObject(device); }
                }
            }
            finally { Marshal.ReleaseComObject(collection); }
            devices.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return devices;
        }

        public AudioDevice GetDefault(EDataFlow flow)
        {
            IMMDevice device;
            if (enumerator.GetDefaultAudioEndpoint(flow, ERole.Console, out device) != 0 || device == null)
                return null;
            try { return Describe(device, flow); }
            finally { Marshal.ReleaseComObject(device); }
        }

        public bool IsActive(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            IMMDevice device;
            if (enumerator.GetDevice(id, out device) != 0 || device == null) return false;
            try
            {
                int state;
                return device.GetState(out state) == 0 && state == DeviceStateActive;
            }
            finally { Marshal.ReleaseComObject(device); }
        }

        public AudioDevice Find(string id, EDataFlow flow)
        {
            if (!IsActive(id)) return null;
            IMMDevice device;
            if (enumerator.GetDevice(id, out device) != 0 || device == null) return null;
            try { return Describe(device, flow); }
            finally { Marshal.ReleaseComObject(device); }
        }

        // Cambia el predeterminado para los tres roles, así también lo siguen Teams, Zoom y Discord.
        public bool SetDefault(string id)
        {
            IPolicyConfig policy = (IPolicyConfig)new PolicyConfigComObject();
            try
            {
                bool ok = policy.SetDefaultEndpoint(id, ERole.Console) == 0;
                ok &= policy.SetDefaultEndpoint(id, ERole.Multimedia) == 0;
                ok &= policy.SetDefaultEndpoint(id, ERole.Communications) == 0;
                return ok;
            }
            finally { Marshal.ReleaseComObject(policy); }
        }

        public bool? GetMute(string id)
        {
            IAudioEndpointVolume volume = GetVolume(id);
            if (volume == null) return null;
            bool muted;
            if (volume.GetMute(out muted) != 0)
            {
                DropVolume(id);
                return null;
            }
            return muted;
        }

        public bool SetMute(string id, bool mute)
        {
            IAudioEndpointVolume volume = GetVolume(id);
            if (volume == null) return false;
            Guid context = Guid.Empty;
            if (volume.SetMute(mute, ref context) == 0) return true;
            DropVolume(id);
            return false;
        }

        IAudioEndpointVolume GetVolume(string id)
        {
            IAudioEndpointVolume volume;
            if (volumeCache.TryGetValue(id, out volume)) return volume;

            IMMDevice device;
            if (enumerator.GetDevice(id, out device) != 0 || device == null) return null;
            try
            {
                Guid iid = AudioEndpointVolumeId;
                object instance;
                if (device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out instance) != 0 || instance == null)
                    return null;
                volume = (IAudioEndpointVolume)instance;
                volumeCache[id] = volume;
                return volume;
            }
            finally { Marshal.ReleaseComObject(device); }
        }

        void DropVolume(string id)
        {
            IAudioEndpointVolume volume;
            if (!volumeCache.TryGetValue(id, out volume)) return;
            volumeCache.Remove(id);
            try { Marshal.ReleaseComObject(volume); } catch { }
        }

        AudioDevice Describe(IMMDevice device, EDataFlow flow)
        {
            string id;
            if (device.GetId(out id) != 0 || id == null) return null;

            AudioDevice cached;
            if (deviceCache.TryGetValue(id, out cached)) return cached;

            AudioDevice info = new AudioDevice();
            info.Id = id;
            info.Flow = flow;
            info.Name = ReadString(device, DeviceFriendlyName) ?? id;
            info.InterfaceName = ReadString(device, DeviceInterfaceFriendlyName) ?? "";
            deviceCache[id] = info;
            return info;
        }

        static string ReadString(IMMDevice device, PropertyKey key)
        {
            IPropertyStore store;
            if (device.OpenPropertyStore(StgmRead, out store) != 0 || store == null) return null;
            try
            {
                PropVariant value;
                if (store.GetValue(ref key, out value) != 0) return null;
                try { return value.VarType == VtLpwstr ? Marshal.PtrToStringUni(value.Value) : null; }
                finally { NativeMethods.PropVariantClear(ref value); }
            }
            finally { Marshal.ReleaseComObject(store); }
        }
    }
}
