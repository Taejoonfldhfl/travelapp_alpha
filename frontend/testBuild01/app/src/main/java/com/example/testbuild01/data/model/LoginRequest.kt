package com.example.testbuild01.data.model
import com.google.gson.annotations.SerializedName

data class LoginRequest (
    @SerializedName("Email")
    val email: String,
    @SerializedName("Password")
    val password: String
)

