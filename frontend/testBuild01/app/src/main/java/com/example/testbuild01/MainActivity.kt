package com.example.testbuild01

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.MutableState
import androidx.compose.runtime.mutableStateOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import com.example.testbuild01.ui.LoginScreen
import com.example.testbuild01.ui.MapScreen
import com.example.testbuild01.ui.ProjectSelectionScreen
import com.example.testbuild01.ui.RegisterScreen
import com.example.testbuild01.ui.theme.TestBuild01Theme
import com.example.testbuild01.ui.CreateTripScreen
import com.example.testbuild01.ui.TripMemberScreen
import com.example.testbuild01.ui.ScheduleScreen
import com.example.testbuild01.ui.aichat.AiChatScreen
import com.example.testbuild01.ui.ticket.TicketListScreen
import com.example.testbuild01.ui.ticket.TicketManualEntryScreen
import com.example.testbuild01.ui.ticket.TicketScanScreen
import com.example.testbuild01.ui.ticket.TicketViewModel

class MainActivity : ComponentActivity() {
    // 알림을 탭해 들어온 티켓 id. TravelApp이 소비하면 null로 되돌린다.
    private val pendingTicketId = mutableStateOf<Long?>(null)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        // 화면 회전 등으로 재생성될 때 같은 알림 인텐트를 다시 처리하지 않는다.
        if (savedInstanceState == null) readTicketId(intent)
        setContent {
            TestBuild01Theme {
                TravelApp(pendingTicketId = pendingTicketId)
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        readTicketId(intent)
    }

    private fun readTicketId(intent: Intent?) {
        val id = intent?.getLongExtra(EXTRA_TICKET_ID, -1L) ?: -1L
        if (id >= 0) pendingTicketId.value = id
    }

    companion object {
        const val EXTRA_TICKET_ID = "ticket_id"
    }
}

@Composable
fun TravelApp(pendingTicketId: MutableState<Long?> = mutableStateOf(null)) {
    val navController = rememberNavController()
    // 스캔 → 수동 입력 → 목록이 상태를 공유하도록 Activity 범위로 둔다.
    val ticketViewModel: TicketViewModel = viewModel(
        viewModelStoreOwner = LocalContext.current as ComponentActivity
    )

    NavHost(navController = navController, startDestination = "login") {
        composable("login") {
            LoginScreen(
                onLoginSuccess = {
                    navController.navigate("project_selection")
                },
                onRegisterSelected = {
                    navController.navigate("register")
                }
            )
        }
        composable("register") {
            RegisterScreen(
                onRegisterSuccess = {
                    // 회원가입 성공 시 이전 화면(로그인)으로 돌아가기
                    navController.popBackStack()
                }
            )
        }
        composable("project_selection") {
            ProjectSelectionScreen(onProjectSelected = { trip ->
                navController.navigate("map/${trip.id}/${trip.title}")
            },
                onCreateTripSelected = {
                    navController.navigate("create_trip")
                },
                onTicketsSelected = {
                    navController.navigate("ticket_list")
                })
        }
        composable("create_trip") {
            CreateTripScreen(
                onTripCreated = {
                    navController.navigate("project_selection") {
                        popUpTo("project_selection") {
                            inclusive = true
                        }
                    }
                }
            )
        }
        composable(
            route = "map/{tripId}/{projectName}",
            arguments = listOf(
                navArgument("tripId") {type = NavType.IntType},
                navArgument("projectName") { type = NavType.StringType })
        ) { backStackEntry ->
            val tripId = backStackEntry.arguments?.getInt("tripId") ?: 0
            val projectName = backStackEntry.arguments?.getString("projectName") ?: ""
            MapScreen(
                tripId = tripId,
                projectName = projectName,
                onScheduleSelected = {
                    navController.navigate(
                        "schedule/$tripId"
                    )
                },
                onMemberSelected = {
                    navController.navigate(
                        "members/$tripId"
                    )
                },
                onPlaceRecommendationSelected = {
                    navController.navigate(
                        "ai_chat/$tripId"
                    )
                },
                onTicketScanSelected = {
                    ticketViewModel.startNew()
                    navController.navigate("ticket_scan")
                },
                onTicketListSelected = {
                    navController.navigate("ticket_list")
                })
        }
        composable(
            route = "members/{tripId}",
            arguments = listOf(
                navArgument("tripId") {
                    type = NavType.IntType
                }
            )
        ) { backStackEntry ->
            val tripId =
                backStackEntry.arguments
                    ?.getInt("tripId") ?: 0

            TripMemberScreen(
                tripId = tripId,
                onBack = {
                    navController.popBackStack()
                }
            )
        }
        composable(
            route = "schedule/{tripId}",

            arguments = listOf(
                navArgument("tripId") {
                    type = NavType.IntType
                }
            )
        ) { backStackEntry ->

            val tripId =
                backStackEntry.arguments
                    ?.getInt("tripId") ?: 0

            ScheduleScreen(
                tripId = tripId,

                onBack = {
                    navController.popBackStack()
                }
            )
        }

        composable(
            route = "ai_chat/{tripId}",

            arguments = listOf(
                navArgument("tripId") {
                    type = NavType.IntType
                }
            )
        ) { backStackEntry ->

            val tripId =
                backStackEntry.arguments
                    ?.getInt("tripId") ?: 0

            AiChatScreen(
                tripId = tripId,

                onBack = {
                    navController.popBackStack()
                }
            )
        }

        composable(
            route = "ticket_list?highlightId={highlightId}",
            arguments = listOf(
                navArgument("highlightId") {
                    type = NavType.LongType
                    defaultValue = -1L
                }
            )
        ) { backStackEntry ->
            val highlightId =
                backStackEntry.arguments
                    ?.getLong("highlightId") ?: -1L

            TicketListScreen(
                viewModel = ticketViewModel,
                highlightId = highlightId,
                onAddTicket = {
                    ticketViewModel.startNew()
                    navController.navigate("ticket_scan")
                },
                onEditTicket = { ticket ->
                    ticketViewModel.startEdit(ticket)
                    navController.navigate("ticket_manual_entry")
                },
                onBack = {
                    navController.popBackStack()
                }
            )
        }

        composable("ticket_scan") {
            TicketScanScreen(
                onScanned = { result ->
                    ticketViewModel.onScanned(result)
                    navController.navigate("ticket_manual_entry") {
                        popUpTo("ticket_scan") { inclusive = true }
                    }
                },
                onManualEntry = {
                    navController.navigate("ticket_manual_entry") {
                        popUpTo("ticket_scan") { inclusive = true }
                    }
                },
                onBack = {
                    navController.popBackStack()
                }
            )
        }

        composable("ticket_manual_entry") {
            TicketManualEntryScreen(
                viewModel = ticketViewModel,
                onBack = {
                    ticketViewModel.startNew()
                    navController.popBackStack()
                },
                onSaved = {
                    navController.popBackStack()
                }
            )
        }

    }

    // 알림 탭으로 들어온 경우 해당 티켓이 열린 목록 화면으로 이동한다.
    val ticketId = pendingTicketId.value
    LaunchedEffect(ticketId) {
        if (ticketId != null) {
            val route = "ticket_list?highlightId={highlightId}"
            navController.navigate("ticket_list?highlightId=$ticketId") {
                popUpTo(route) { inclusive = true }
            }
            pendingTicketId.value = null
        }
    }
}
