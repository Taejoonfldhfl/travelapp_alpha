package com.example.testbuild01.data.network

import com.google.gson.JsonSyntaxException
import com.google.gson.stream.MalformedJsonException
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.net.SocketTimeoutException
import java.net.UnknownHostException

class ScheduleRemoteErrorTest {

    @Test
    fun transportFailures_areNetwork() {
        assertEquals(SyncErrorKind.NETWORK, errorKindOf(SocketTimeoutException()))
        assertEquals(SyncErrorKind.NETWORK, errorKindOf(UnknownHostException()))
    }

    @Test
    fun parseFailures_areInvalidResponse_notNetwork() {
        assertEquals(SyncErrorKind.INVALID_RESPONSE, errorKindOf(JsonSyntaxException("bad")))
        assertEquals(SyncErrorKind.INVALID_RESPONSE, errorKindOf(MalformedJsonException("bad")))
        assertEquals(SyncErrorKind.INVALID_RESPONSE, errorKindOf(IllegalStateException("Expected BEGIN_OBJECT")))
    }

    @Test
    fun outcomeUnknown_onlyForFailuresWhereServerMayHaveSaved() {
        assertTrue(SyncError(SyncErrorKind.NETWORK).outcomeUnknown)
        assertTrue(SyncError(SyncErrorKind.SERVER).outcomeUnknown)
        assertTrue(SyncError(SyncErrorKind.INVALID_RESPONSE).outcomeUnknown)
        assertFalse(SyncError(SyncErrorKind.REJECTED).outcomeUnknown)
        assertFalse(SyncError(SyncErrorKind.FORBIDDEN).outcomeUnknown)
    }
}
