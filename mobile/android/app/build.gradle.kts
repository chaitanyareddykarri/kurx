import java.util.Properties

// Loaded before the android {} block so both signingConfigs and buildTypes can see it.
// Absent in a normal dev checkout — only release builds require it.
val releaseKeystoreProperties: Properties? = rootProject.file("key.properties").takeIf { it.exists() }?.let {
    Properties().apply { it.inputStream().use { stream -> load(stream) } }
}

plugins {
    id("com.android.application")
    // Firebase (D-096). Reads app/google-services.json and matches it against applicationId;
    // a mismatch fails the build rather than silently shipping an app that cannot register for push.
    id("com.google.gms.google-services")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

android {
    namespace = "in.kurx.kurx_mobile"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        // TODO: Specify your own unique Application ID (https://developer.android.com/studio/build/application-id.html).
        applicationId = "in.kurx.kurx_mobile"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    // Release signing (D-103a). Credentials come from android/key.properties, which is gitignored —
    // no keystore or password ever enters the repository.
    signingConfigs {
        create("release") {
            if (releaseKeystoreProperties != null) {
                val props = releaseKeystoreProperties!!
                storeFile = file(props.getProperty("storeFile"))
                storePassword = props.getProperty("storePassword")
                keyAlias = props.getProperty("keyAlias")
                keyPassword = props.getProperty("keyPassword")
            }
        }
    }

    buildTypes {
        release {
            // Fail closed. Previously this fell back to `signingConfigs.getByName("debug")`, which
            // signed release builds with the PUBLICLY DISTRIBUTED Android debug key (fixed password
            // "android") — re-signable by anyone, rejected by Play, and for passkeys it would bind
            // credentials to a key that is not exclusively ours. A missing config must now stop the
            // build with an actionable message rather than produce a dangerously-signed artifact.
            if (releaseKeystoreProperties == null) {
                throw GradleException(
                    "Release signing is not configured. Create android/key.properties from " +
                    "android/key.properties.example (see docs/mobile/RELEASE_SIGNING.md). " +
                    "Release builds must never be signed with the debug key."
                )
            }
            signingConfig = signingConfigs.getByName("release")
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
    }
}

dependencies {
    // BiometricPrompt — required to release the AndroidKeyStore signing key (D-091). Provides the
    // biometric-or-device-credential prompt and the CryptoObject binding that ties one
    // authentication to one signature.
    implementation("androidx.biometric:biometric:1.1.0")

    // Passkeys via Credential Manager (D-097). `credentials-play-services-auth` is the provider
    // that actually surfaces Google Password Manager; without it createCredential finds no
    // provider on most devices and fails at runtime rather than at build time.
    implementation("androidx.credentials:credentials:1.3.0")
    implementation("androidx.credentials:credentials-play-services-auth:1.3.0")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.8.1")
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}
