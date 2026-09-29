package com.example.testbuild01

import android.content.Intent
import android.os.Bundle
import android.widget.Toast
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
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import com.example.testbuild01.data.local.TokenManager
import com.example.testbuild01.data.network.AuthEvents
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
import com.example.testbuild01.data.local.TicketType
import com.example.testbuild01.data.repository.TicketDraft
import com.example.testbuild01.ui.aichat.AiChatScreen
import com.example.testbuild01.ui.hotel.HotelConfirmationScannerScreen
import com.example.testbuild01.ui.hotel.HotelDetailReviewScreen
import com.example.testbuild01.ui.hotel.HotelListScreen
import com.example.testbuild01.ui.hotel.HotelManualEntryScreen
import com.example.testbuild01.ui.hotel.HotelSearchScreen
import com.example.testbuild01.data.hotel.HotelOcrCandidate
import com.example.testbuild01.data.local.HotelDetail
import com.example.testbuild01.ui.ticket.TicketListScreen
import com.example.testbuild01.ui.ticket.TicketManualEntryScreen
import com.example.testbuild01.ui.ticket.TicketOcrScannerScreen
import com.example.testbuild01.ui.ticket.TicketScanScreen
import com.example.testbuild01.ui.ticket.TicketViewModel
import android.net.Uri
import com.example.testbuild01.ui.expense.ExpenseEditScreen
import com.example.testbuild01.ui.expense.ExpenseListScreen
import com.example.testbuild01.ui.expense.ExpenseStatsScreen
import com.example.testbuild01.ui.expense.ExpenseViewModel
import com.example.testbuild01.ui.expense.MyExpenseScreen
import com.example.testbuild01.ui.expense.ReceiptScannerScreen
import com.example.testbuild01.ui.expense.SettlementScreen

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
    val context = LocalContext.current
    val tokenManager = remember { TokenManager(context) }
    // 유효한(만료 안 된) 토큰이 있으면 바로 프로젝트 선택으로 건너뛰고, 없거나 만료됐으면
    // 남아있는 토큰을 지우고 로그인부터 시작한다. remember로 감싸 최초 1회만 판단한다.
    val startDestination = remember {
        if (tokenManager.isLoggedIn()) {
            "project_selection"
        } else {
            tokenManager.clearToken()
            "login"
        }
    }
    // 스캔 → 수동 입력 → 목록이 상태를 공유하도록 Activity 범위로 둔다.
    val ticketViewModel: TicketViewModel = viewModel(
        viewModelStoreOwner = LocalContext.current as ComponentActivity,
        factory = TicketViewModel.Factory
    )
    // 가계부 관련 화면들이 공유하는 ViewModel (Activity 범위)
    val expenseViewModel: ExpenseViewModel = viewModel(
        viewModelStoreOwner = LocalContext.current as ComponentActivity
    )

    // 인증이 필요한 요청이 401을 받아 토큰이 지워졌다는 알림을 받으면, 토스트를 띄우고 로그인
    // 화면으로 되돌린다. popUpTo(0)으로 백스택을 전부 비워 뒤로가기로 이전 화면이 보이지 않게 한다.
    LaunchedEffect(Unit) {
        AuthEvents.sessionExpired.collect {
            Toast.makeText(context, "로그인이 만료되었습니다. 다시 로그인해 주세요.", Toast.LENGTH_LONG).show()
            navController.navigate("login") {
                popUpTo(0) { inclusive = true }
            }
        }
    }

    NavHost(navController = navController, startDestination = startDestination) {
        composable("login") {
            LoginScreen(
                onLoginSuccess = {
                    // 뒤로가기를 눌러도 로그인 화면으로 돌아가지 않도록 백스택에서 지운다.
                    navController.navigate("project_selection") {
                        popUpTo("login") { inclusive = true }
                    }
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
                },
                onLogout = {
                    navController.navigate("login") {
                        popUpTo(0) { inclusive = true }
                    }
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
                },
                onExpenseSelected = {
                    navController.navigate("expense_list/$tripId/${Uri.encode(projectName)}")
                },
                onHotelListSelected = {
                    navController.navigate("hotel_list")
                })
        }

        composable("hotel_list") {
            HotelListScreen(onBack = { navController.popBackStack() })
        }
        composable(
            route = "expense_list/{tripId}/{title}",
            arguments = listOf(
                navArgument("tripId") { type = NavType.IntType },
                navArgument("title") { type = NavType.StringType }
            )
        ) { entry ->
            val tripId = entry.arguments?.getInt("tripId") ?: 0
            val title = entry.arguments?.getString("title") ?: ""
            ExpenseListScreen(
                viewModel = expenseViewModel,
                tripId = tripId,
                onBack = { navController.popBackStack() },
                onScan = { navController.navigate("receipt_scan/$tripId") },
                onAdd = {
                    expenseViewModel.updateScannedAmount(null)
                    navController.navigate("expense_edit/0")
                },
                onEdit = { id -> navController.navigate("expense_edit/$id") },
                onStats = { navController.navigate("expense_stats") },
                onSettlement = { navController.navigate("expense_settlement/${Uri.encode(title)}") },
                onMine = { navController.navigate("expense_mine") }
            )
        }
        composable("expense_mine") {
            MyExpenseScreen(viewModel = expenseViewModel, onBack = { navController.popBackStack() })
        }
        composable(
            route = "receipt_scan/{tripId}",
            arguments = listOf(navArgument("tripId") { type = NavType.IntType })
        ) { entry ->
            val tripId = entry.arguments?.getInt("tripId") ?: 0
            LaunchedEffect(tripId) { expenseViewModel.load(tripId) }
            ReceiptScannerScreen(
                viewModel = expenseViewModel,
                onBack = { navController.popBackStack() },
                onConfirm = { navController.navigate("expense_edit/0") }
            )
        }
        composable(
            route = "expense_edit/{expenseId}",
            arguments = listOf(navArgument("expenseId") { type = NavType.IntType })
        ) { entry ->
            val id = entry.arguments?.getInt("expenseId") ?: 0
            ExpenseEditScreen(
                viewModel = expenseViewModel,
                expenseId = id.takeIf { it != 0 },
                onBack = { navController.popBackStack() }
            )
        }
        composable("expense_stats") {
            ExpenseStatsScreen(viewModel = expenseViewModel, onBack = { navController.popBackStack() })
        }
        composable(
            route = "expense_settlement/{title}",
            arguments = listOf(navArgument("title") { type = NavType.StringType })
        ) { entry ->
            SettlementScreen(
                viewModel = expenseViewModel,
                tripTitle = entry.arguments?.getString("title") ?: "",
                onBack = { navController.popBackStack() }
            )
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
                onAddHotel = {
                    ticketViewModel.startNew()
                    navController.navigate("hotel_confirmation_scan")
                },
                onSearchHotels = {
                    navController.navigate("hotel_search")
                },
                onEditTicket = { ticket ->
                    ticketViewModel.startEdit(ticket)
                    if (ticket.type == TicketType.HOTEL) {
                        navController.navigate("hotel_manual_entry")
                    } else {
                        navController.navigate("ticket_manual_entry")
                    }
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
                onOcrScan = {
                    navController.navigate("ticket_ocr_scan") {
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

        composable("ticket_ocr_scan") {
            TicketOcrScannerScreen(
                onRecognized = { candidate ->
                    ticketViewModel.onTicketOcrScanned(candidate)
                    navController.navigate("ticket_manual_entry") {
                        popUpTo("ticket_ocr_scan") { inclusive = true }
                    }
                },
                onManualEntry = {
                    navController.navigate("ticket_manual_entry") {
                        popUpTo("ticket_ocr_scan") { inclusive = true }
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

        composable("hotel_search") {
            HotelSearchScreen(onBack = { navController.popBackStack() })
        }

        composable("hotel_confirmation_scan") {
            HotelConfirmationScannerScreen(
                onRecognized = { candidate ->
                    ticketViewModel.onHotelScanned(candidate)
                    navController.navigate("hotel_detail_review")
                },
                onManualEntry = { navController.navigate("hotel_manual_entry") },
                onBack = { navController.popBackStack() }
            )
        }

        composable("hotel_detail_review") {
            HotelDetailReviewScreen(
                candidate = ticketViewModel.hotelOcrCandidate ?: HotelOcrCandidate(),
                onBack = { navController.popBackStack() },
                onSave = { detail -> saveHotelDetail(ticketViewModel, navController, detail) }
            )
        }

        composable("hotel_manual_entry") {
            HotelManualEntryScreen(
                editing = ticketViewModel.editingTicket?.takeIf { it.type == TicketType.HOTEL }?.hotel,
                onBack = {
                    ticketViewModel.startNew()
                    navController.popBackStack()
                },
                onSave = { detail -> saveHotelDetail(ticketViewModel, navController, detail) }
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

/** 호텔 등록/수정 화면 공통 저장 로직. 저장 후 티켓 목록으로 돌아간다. */
private fun saveHotelDetail(
    ticketViewModel: TicketViewModel,
    navController: androidx.navigation.NavHostController,
    detail: HotelDetail
) {
    val draft = TicketDraft(
        type = TicketType.HOTEL,
        title = detail.hotelName,
        startDateTime = detail.checkInTime,
        locationFrom = detail.address,
        locationTo = "",
        confirmationNumber = detail.confirmationNumber,
        hotel = detail
    )
    ticketViewModel.save(draft) {
        // 검색/스캔을 거치며 여러 화면이 쌓였을 수 있으므로 이미 백스택에 있는 목록 화면까지 한 번에 되돌아간다.
        navController.popBackStack("ticket_list?highlightId={highlightId}", inclusive = false)
    }
}
