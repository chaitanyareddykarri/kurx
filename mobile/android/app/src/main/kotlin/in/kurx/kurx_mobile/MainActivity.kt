package `in`.kurx.kurx_mobile

import io.flutter.embedding.android.FlutterFragmentActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

/**
 * Hosts the Flutter engine and registers the device-key channel (D-091).
 *
 * Extends [FlutterFragmentActivity] rather than `FlutterActivity` because `BiometricPrompt`
 * requires a `FragmentActivity` to attach its dialog fragment — plain `FlutterActivity` compiles
 * fine and then throws at the first biometric prompt, which would only surface on a real device.
 */
class MainActivity : FlutterFragmentActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, DeviceKeyPlugin.CHANNEL)
            .setMethodCallHandler(DeviceKeyPlugin(this))

        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, PasskeyPlugin.CHANNEL)
            .setMethodCallHandler(PasskeyPlugin(this))
    }
}
