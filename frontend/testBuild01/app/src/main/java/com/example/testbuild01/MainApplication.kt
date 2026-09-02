package com.example.testbuild01

import android.app.Application
import com.example.testbuild01.data.network.RetrofitClient
import com.google.android.gms.maps.MapsInitializer
import com.google.android.gms.maps.OnMapsSdkInitializedCallback

class MainApplication : Application(), OnMapsSdkInitializedCallback {
    override fun onCreate() {
        super.onCreate()

        RetrofitClient.init(this)
        // Force the use of the latest renderer which often solves emulator display issues
        MapsInitializer.initialize(applicationContext, MapsInitializer.Renderer.LATEST, this)
    }

    override fun onMapsSdkInitialized(renderer: MapsInitializer.Renderer) {
        when (renderer) {
            MapsInitializer.Renderer.LATEST -> println("Google Maps: The latest version of the renderer is used.")
            MapsInitializer.Renderer.LEGACY -> println("Google Maps: The legacy version of the renderer is used.")
        }
    }
}
