package com.example.testbuild01.ui.hotel

import android.content.Context
import android.net.Uri
import androidx.browser.customtabs.CustomTabsIntent

/**
 * 아고다 검색 결과로 연결하는 딥링크. 실제 결제/확정은 앱 밖(Custom Tabs)에서 이루어지며 이 앱은 관여하지 않는다.
 *
 * 아고다는 자유 텍스트 검색을 자바스크립트 자동완성으로 내부 city/hotel ID를 먼저 얻은 뒤 그 ID로
 * 이동하는 방식이라, agoda.com에 직접 `?q=호텔명` 같은 파라미터를 넘겨도 실제로 검색이 되지 않는다.
 * 대신 구글 사이트검색(site:agoda.com)을 경유해 사용자가 정확한 결과 페이지를 고를 수 있게 한다.
 */
object AgodaDeepLink {
    fun searchUrl(hotelName: String): Uri =
        Uri.parse("https://www.google.com/search?q=" + Uri.encode("site:agoda.com $hotelName"))

    fun open(context: Context, hotelName: String) {
        CustomTabsIntent.Builder().build().launchUrl(context, searchUrl(hotelName))
    }
}
