package com.example.testbuild01

import android.app.Application
import com.example.testbuild01.data.local.TicketDatabase
import com.example.testbuild01.data.local.TinkTicketCipher
import com.example.testbuild01.data.repository.HotelScheduleLinker
import com.example.testbuild01.data.repository.TicketRepository
import com.example.testbuild01.notification.AndroidAlarmBackend
import com.example.testbuild01.notification.FcmTokenRegistrar
import com.example.testbuild01.notification.TicketAlarmScheduler
import com.example.testbuild01.notification.TicketNotifier
import com.example.testbuild01.data.network.RetrofitClient
import com.example.testbuild01.data.network.RetrofitScheduleRemote
import com.google.android.gms.maps.MapsInitializer
import com.google.android.gms.maps.OnMapsSdkInitializedCallback

class MainApplication : Application(), OnMapsSdkInitializedCallback {
    val ticketRepository: TicketRepository by lazy {
        TicketRepository(
            dao = TicketDatabase.create(this).ticketDao(),
            cipher = TinkTicketCipher(this),
            scheduler = TicketAlarmScheduler(AndroidAlarmBackend(this))
        )
    }

    val hotelScheduleLinker: HotelScheduleLinker by lazy {
        HotelScheduleLinker(repository = ticketRepository, remote = RetrofitScheduleRemote())
    }

    override fun onCreate() {
        super.onCreate()
        TicketNotifier.createChannel(this)

        RetrofitClient.init(this)
        // FCM 토큰 등록: 로그인 전이면 서버가 401을 주고 끝나며(로그인 후 LoginScreen에서 다시 시도),
        // google-services.json이 없으면(Firebase 미설정) 내부적으로 조용히 건너뛴다.
        FcmTokenRegistrar.registerCurrentToken(this)
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
