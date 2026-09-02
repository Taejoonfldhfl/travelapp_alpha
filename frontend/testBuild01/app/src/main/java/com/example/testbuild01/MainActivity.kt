package com.example.testbuild01

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
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

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            TestBuild01Theme {
                TravelApp()
            }
        }
    }
}

@Composable
fun TravelApp() {
    val navController = rememberNavController()

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

    }
}
