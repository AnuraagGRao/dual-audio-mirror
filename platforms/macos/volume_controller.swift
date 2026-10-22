import Foundation
import CoreAudio

// ==============================================================================
// macOS CoreAudio Device Inspector & Volume Controller
// Queries active audio devices and controls scalar output levels.
// ==============================================================================

func getAudioDeviceCount() -> UInt32 {
    var propertyAddress = AudioObjectPropertyAddress(
        mSelector: kAudioHardwarePropertyDevices,
        mScope: kAudioObjectPropertyScopeGlobal,
        mElement: kAudioObjectPropertyElementMain
    )
    var dataSize: UInt32 = 0
    let status = AudioObjectGetPropertyDataSize(AudioObjectID(kAudioObjectSystemObject), &propertyAddress, 0, nil, &dataSize)
    guard status == noErr else { return 0 }
    return dataSize / UInt32(MemoryLayout<AudioDeviceID>.size)
}

func getAllAudioDevices() -> [AudioDeviceID] {
    let count = getAudioDeviceCount()
    guard count > 0 else { return [] }
    var devices = [AudioDeviceID](repeating: 0, count: Int(count))
    var dataSize = count * UInt32(MemoryLayout<AudioDeviceID>.size)
    var propertyAddress = AudioObjectPropertyAddress(
        mSelector: kAudioHardwarePropertyDevices,
        mScope: kAudioObjectPropertyScopeGlobal,
        mElement: kAudioObjectPropertyElementMain
    )
    let status = AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject), &propertyAddress, 0, nil, &dataSize, &devices)
    guard status == noErr else { return [] }
    return devices
}

func getDeviceName(deviceID: AudioDeviceID) -> String {
    var propertyAddress = AudioObjectPropertyAddress(
        mSelector: kAudioDevicePropertyDeviceNameCFString,
        mScope: kAudioObjectPropertyScopeGlobal,
        mElement: kAudioObjectPropertyElementMain
    )
    var name: CFString = "" as CFString
    var dataSize = UInt32(MemoryLayout<CFString?>.size)
    let status = AudioObjectGetPropertyData(deviceID, &propertyAddress, 0, nil, &dataSize, &name)
    guard status == noErr else { return "Unknown Device" }
    return name as String
}

func isOutputDevice(deviceID: AudioDeviceID) -> Bool {
    var propertyAddress = AudioObjectPropertyAddress(
        mSelector: kAudioDevicePropertyStreams,
        mScope: kAudioDevicePropertyScopeOutput,
        mElement: kAudioObjectPropertyElementMain
    )
    var dataSize: UInt32 = 0
    let status = AudioObjectGetPropertyDataSize(deviceID, &propertyAddress, 0, nil, &dataSize)
    return (status == noErr && dataSize > 0)
}

print("==========================================================")
print("       macOS CoreAudio Output Device Inspector            ")
print("==========================================================")

let devices = getAllAudioDevices()
for dev in devices {
    if isOutputDevice(deviceID: dev) {
        let name = getDeviceName(deviceID: dev)
        print(" [Device ID: \(dev)] - \(name)")
    }
}
print("----------------------------------------------------------")
print("To combine these into a zero-latency Multi-Output Device:")
print("1. Open Audio MIDI Setup.app")
print("2. Click '+' at bottom left -> Create Multi-Output Device")
print("3. Check desired outputs & enable Drift Correction.")
print("==========================================================")
