// Global variables
let connection;
let weightChart;
let distributionChart;
let readings = [];

// Initialize the application
document.addEventListener('DOMContentLoaded', function() {
    initializeSignalR();
    initializeCharts();
    loadInitialData();
});

// SignalR Connection
function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/weighthub")
        .withAutomaticReconnect()
        .build();

    // Handle new weight readings
    connection.on("ReceiveWeightUpdate", function (reading) {
        addNewReading(reading);
        showNewReadingToast(reading);
    });

    // Handle stats updates
    connection.on("ReceiveStatsUpdate", function (stats) {
        updateDashboardStats(stats);
    });

    // Connection events
    connection.onreconnecting(() => {
        updateConnectionStatus('Reconnecting...', 'warning');
    });

    connection.onreconnected(() => {
        updateConnectionStatus('Connected', 'success');
    });

    connection.onclose(() => {
        updateConnectionStatus('Disconnected', 'danger');
    });

    // Start connection
    connection.start()
        .then(() => {
            updateConnectionStatus('Connected', 'success');
        })
        .catch(err => {
            console.error('SignalR connection error:', err);
            updateConnectionStatus('Connection Failed', 'danger');
        });
}

// Initialize Charts
function initializeCharts() {
    // Weight Trend Chart
    const weightCtx = document.getElementById('weightChart').getContext('2d');
    weightChart = new Chart(weightCtx, {
        type: 'line',
        data: {
            labels: [],
            datasets: [{
                label: 'Weight (kg)',
                data: [],
                borderColor: 'rgb(75, 192, 192)',
                backgroundColor: 'rgba(75, 192, 192, 0.1)',
                tension: 0.1,
                fill: true
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Weight (kg)'
                    }
                },
                x: {
                    title: {
                        display: true,
                        text: 'Time'
                    }
                }
            },
            plugins: {
                legend: {
                    display: true,
                    position: 'top'
                }
            }
        }
    });

    // Distribution Chart
    const distributionCtx = document.getElementById('distributionChart').getContext('2d');
    distributionChart = new Chart(distributionCtx, {
        type: 'doughnut',
        data: {
            labels: ['Empty', 'Low', 'Normal', 'High'],
            datasets: [{
                data: [0, 0, 0, 0],
                backgroundColor: [
                    '#6c757d',
                    '#ffc107',
                    '#28a745',
                    '#dc3545'
                ]
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'bottom'
                }
            }
        }
    });
}

// Load initial data
async function loadInitialData() {
    try {
        // Load stats
        const statsResponse = await fetch('/api/weight/stats');
        const stats = await statsResponse.json();
        updateDashboardStats(stats);

        // Load recent readings
        const readingsResponse = await fetch('/api/weight/recent?count=50');
        const recentReadings = await readingsResponse.json();
        
        readings = recentReadings;
        updateReadingsTable();
        updateCharts();
    } catch (error) {
        console.error('Error loading initial data:', error);
    }
}

// Update dashboard stats
function updateDashboardStats(stats) {
    document.getElementById('currentWeight').textContent = `${stats.currentWeight.toFixed(2)} kg`;
    document.getElementById('averageWeight').textContent = `${stats.averageWeight.toFixed(2)} kg`;
    document.getElementById('totalReadings').textContent = stats.totalReadings;
    
    const statusElement = document.getElementById('systemStatus');
    const statusCard = document.getElementById('statusCard');
    const statusIcon = document.getElementById('statusIcon');
    
    statusElement.textContent = stats.status;
    
    // Update status card color based on status
    statusCard.className = 'card';
    statusIcon.className = 'fas fa-2x';
    
    switch (stats.status.toLowerCase()) {
        case 'normal':
            statusCard.classList.add('bg-success', 'text-white');
            statusIcon.classList.add('fa-check-circle');
            break;
        case 'low':
            statusCard.classList.add('bg-warning', 'text-dark');
            statusIcon.classList.add('fa-exclamation-triangle');
            break;
        case 'high':
            statusCard.classList.add('bg-danger', 'text-white');
            statusIcon.classList.add('fa-exclamation-circle');
            break;
        case 'empty':
            statusCard.classList.add('bg-secondary', 'text-white');
            statusIcon.classList.add('fa-battery-empty');
            break;
        default:
            statusCard.classList.add('bg-light');
            statusIcon.classList.add('fa-question-circle');
    }
}

// Add new reading to the list
function addNewReading(reading) {
    readings.unshift(reading);
    if (readings.length > 100) {
        readings = readings.slice(0, 100); // Keep only last 100 readings
    }
    
    updateReadingsTable();
    updateCharts();
}

// Update readings table
function updateReadingsTable() {
    const tbody = document.getElementById('readingsTableBody');
    
    if (readings.length === 0) {
        tbody.innerHTML = '<tr><td colspan="5" class="text-center text-muted">No readings available</td></tr>';
        return;
    }
    
    tbody.innerHTML = readings.slice(0, 20).map(reading => {
        const timestamp = new Date(reading.timestamp).toLocaleString();
        const statusBadge = getStatusBadge(reading.status);
        
        return `
            <tr>
                <td>${reading.id}</td>
                <td>${reading.weight.toFixed(3)}</td>
                <td>${reading.deviceId}</td>
                <td>${statusBadge}</td>
                <td>${timestamp}</td>
            </tr>
        `;
    }).join('');
}

// Get status badge HTML
function getStatusBadge(status) {
    const badgeClass = {
        'Normal': 'badge-success',
        'Low': 'badge-warning',
        'High': 'badge-danger',
        'Empty': 'badge-secondary',
        'Error': 'badge-danger'
    }[status] || 'badge-secondary';
    
    return `<span class="badge ${badgeClass}">${status}</span>`;
}

// Update charts
function updateCharts() {
    if (readings.length === 0) return;
    
    // Update weight trend chart
    const last20 = readings.slice(0, 20).reverse();
    weightChart.data.labels = last20.map(r => new Date(r.timestamp).toLocaleTimeString());
    weightChart.data.datasets[0].data = last20.map(r => r.weight);
    weightChart.update('none');
    
    // Update distribution chart
    const statusCounts = {
        'Empty': 0,
        'Low': 0,
        'Normal': 0,
        'High': 0
    };
    
    readings.forEach(reading => {
        if (statusCounts.hasOwnProperty(reading.status)) {
            statusCounts[reading.status]++;
        }
    });
    
    distributionChart.data.datasets[0].data = Object.values(statusCounts);
    distributionChart.update('none');
}

// Show new reading toast
function showNewReadingToast(reading) {
    const toastBody = document.getElementById('toastBody');
    toastBody.textContent = `New reading: ${reading.weight.toFixed(2)} kg (${reading.status})`;
    
    const toast = new bootstrap.Toast(document.getElementById('newReadingToast'));
    toast.show();
}

// Update connection status
function updateConnectionStatus(status, type) {
    const statusElement = document.getElementById('connectionStatus');
    statusElement.textContent = status;
    statusElement.className = `badge badge-${type}`;
}

// Refresh data manually
async function refreshData() {
    await loadInitialData();
}

// Utility function to format dates
function formatDate(dateString) {
    return new Date(dateString).toLocaleString();
}
