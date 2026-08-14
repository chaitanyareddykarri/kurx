allprojects {
    repositories {
        google()
        mavenCentral()
    }
}

val newBuildDir: Directory =
    rootProject.layout.buildDirectory
        .dir("../../build")
        .get()
rootProject.layout.buildDirectory.value(newBuildDir)

subprojects {
    val newSubprojectBuildDir: Directory = newBuildDir.dir(project.name)
    project.layout.buildDirectory.value(newSubprojectBuildDir)
}
subprojects {
    project.evaluationDependsOn(":app")
}

// file_picker 11.0.2 deliberately skips applying its own Kotlin Gradle Plugin under AGP 9+, expecting
// Flutter's built-in Kotlin to compile its FilePickerPlugin.kt. We keep built-in Kotlin off (for
// mobile_scanner, which applies its own KGP and cannot coexist with built-in Kotlin), so without this
// nothing compiles file_picker's Kotlin and the release build fails with "cannot find symbol:
// FilePickerPlugin". Applying the plugin here — before file_picker's own build script evaluates —
// restores its Kotlin compilation while leaving every other plugin untouched. (Phase 7 fix.)
subprojects {
    if (name == "file_picker") {
        apply(plugin = "org.jetbrains.kotlin.android")
    }
}

tasks.register<Delete>("clean") {
    delete(rootProject.layout.buildDirectory)
}
