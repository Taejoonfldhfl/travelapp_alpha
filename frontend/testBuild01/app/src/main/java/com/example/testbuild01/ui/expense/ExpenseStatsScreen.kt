@file:OptIn(ExperimentalMaterial3Api::class)

package com.example.testbuild01.ui.expense

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.model.ExpenseBreakdownItem
import com.example.testbuild01.data.model.ExpenseCategory
import com.patrykandpatrick.vico.compose.cartesian.CartesianChartHost
import com.patrykandpatrick.vico.compose.cartesian.axis.rememberBottom
import com.patrykandpatrick.vico.compose.cartesian.axis.rememberStart
import com.patrykandpatrick.vico.compose.cartesian.layer.rememberColumnCartesianLayer
import com.patrykandpatrick.vico.compose.cartesian.rememberCartesianChart
import com.patrykandpatrick.vico.core.cartesian.axis.HorizontalAxis
import com.patrykandpatrick.vico.core.cartesian.axis.VerticalAxis
import com.patrykandpatrick.vico.core.cartesian.data.CartesianChartModelProducer
import com.patrykandpatrick.vico.core.cartesian.data.CartesianValueFormatter
import com.patrykandpatrick.vico.core.cartesian.data.columnSeries

private val categoryColors = mapOf(
    ExpenseCategory.FOOD to Color(0xFFEF6C00),
    ExpenseCategory.TRANSPORT to Color(0xFF1E88E5),
    ExpenseCategory.LODGING to Color(0xFF8E24AA),
    ExpenseCategory.SHOPPING to Color(0xFFD81B60),
    ExpenseCategory.ETC to Color(0xFF757575)
)

@Composable
fun ExpenseStatsScreen(viewModel: ExpenseViewModel, onBack: () -> Unit) {
    LaunchedEffect(Unit) { viewModel.refresh() }
    val items = viewModel.breakdown

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("지출 통계") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "뒤로 가기")
                    }
                }
            )
        }
    ) { padding ->
        Column(
            modifier = Modifier.padding(padding).padding(16.dp).fillMaxWidth(),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            BudgetSummaryCard(viewModel.summary, onEditBudget = {})

            if (items.isEmpty()) {
                Text("통계를 낼 지출이 없습니다.")
                return@Column
            }

            Text("카테고리별 비중", style = MaterialTheme.typography.titleMedium)
            DonutChart(items, modifier = Modifier.size(200.dp))

            items.forEach { item ->
                Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.fillMaxWidth()) {
                    Box(
                        Modifier.size(12.dp).background(categoryColors[item.category] ?: Color.Gray, CircleShape)
                    )
                    Spacer(Modifier.width(8.dp))
                    Text(item.category.label, modifier = Modifier.weight(1f))
                    Text("${won(item.total)} (${"%.1f".format(item.ratio * 100)}%)")
                }
            }

            Text("카테고리별 금액", style = MaterialTheme.typography.titleMedium)
            CategoryColumnChart(items)
        }
    }
}

// Vico 는 카테시안(막대/선) 차트 중심이라 도넛 차트가 없어서 Canvas 로 직접 그린다.
@Composable
fun DonutChart(items: List<ExpenseBreakdownItem>, modifier: Modifier = Modifier) {
    val total = items.sumOf { it.total }.takeIf { it > 0 } ?: return
    Canvas(modifier = modifier) {
        val strokeWidth = size.minDimension * 0.22f
        val inset = strokeWidth / 2
        val arcSize = Size(size.width - strokeWidth, size.height - strokeWidth)
        var start = -90f
        items.forEach { item ->
            val sweep = (item.total / total * 360.0).toFloat()
            drawArc(
                color = categoryColors[item.category] ?: Color.Gray,
                startAngle = start,
                sweepAngle = sweep,
                useCenter = false,
                topLeft = Offset(inset, inset),
                size = arcSize,
                style = Stroke(width = strokeWidth, cap = StrokeCap.Butt)
            )
            start += sweep
        }
    }
}

@Composable
private fun CategoryColumnChart(items: List<ExpenseBreakdownItem>) {
    val producer = remember { CartesianChartModelProducer() }
    LaunchedEffect(items) {
        producer.runTransaction {
            columnSeries { series(items.map { it.total }) }
        }
    }
    val labelFormatter = remember(items) {
        CartesianValueFormatter { _, x, _ -> items.getOrNull(x.toInt())?.category?.label ?: "" }
    }
    CartesianChartHost(
        chart = rememberCartesianChart(
            rememberColumnCartesianLayer(),
            startAxis = VerticalAxis.rememberStart(),
            bottomAxis = HorizontalAxis.rememberBottom(valueFormatter = labelFormatter)
        ),
        modelProducer = producer,
        modifier = Modifier.fillMaxWidth().height(200.dp)
    )
}
