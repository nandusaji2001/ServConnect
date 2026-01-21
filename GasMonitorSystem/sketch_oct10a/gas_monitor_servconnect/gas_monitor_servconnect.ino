/*
 * ============================================================
 * ServConnect Gas Cylinder Monitoring System
 * ============================================================
 * 
 * This ESP32 code monitors gas cylinder weight using a load cell
 * and HX711 amplifier, sending data to ServConnect backend for
 * automatic gas booking when levels fall below threshold.
 * 
 * Hardware Required:
 * - ESP32 DevKit
 * - 2kg Load Cell
 * - HX711 Load Cell Amplifier
 * 
 * Wiring:
 * - HX711 DT  -> ESP32 GPIO 4
 * - HX711 SCK -> ESP32 GPIO 5
 * - HX711 VCC -> ESP32 3.3V
 * - HX711 GND -> ESP32 GND
 * 
 * Features:
 * - Real-time weight monitoring
 * - WiFi connectivity with auto-reconnect
 * - Configurable calibration
 * - LED status indicators
 * - Low gas level alerts
 * - Automatic API posting
 * 
 * Author: ServConnect Team
 * Version: 2.0.0
 * ============================================================
 */

#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include "HX711.h"

// ============================================================
// CONFIGURATION - MODIFY THESE VALUES
// ============================================================

// WiFi Configuration
const char* WIFI_SSID     = "Darki";       // Your WiFi network name
const char* WIFI_PASSWORD = "nandusaji123";   // Your WiFi password

// API Configuration
// Use your local IP for development, or your server URL for production
const char* API_ENDPOINT  = "http://10.13.28.209:5227/api/GasSubscriptionApi/reading";
const char* DEVICE_ID     = "ESP32-GAS-001";        // Unique device ID

// Timing Configuration (in milliseconds)
const unsigned long POST_INTERVAL_MS = 10000;       // Send data every 10 seconds
const unsigned long WIFI_RETRY_INTERVAL_MS = 30000; // Retry WiFi every 30 seconds

// Weight Configuration for 2kg Load Cell
const float FULL_CYLINDER_WEIGHT_KG = 2.0;          // Weight when cylinder is full
const float EMPTY_CYLINDER_WEIGHT_KG = 0.5;         // Tare weight of empty cylinder
const float MIN_WEIGHT_DELTA_FOR_SEND = 0.02f;      // Only send if weight changed by 20g

// Calibration
const float MIN_CALIBRATION_WEIGHT_GRAM = 50.0f;    // Minimum weight for calibration

// LED Pins (optional - for status indication)
#define LED_WIFI    2   // Built-in LED for WiFi status
#define LED_STATUS  -1  // External LED for gas status (-1 to disable)

// HX711 Pins
#define HX711_DT    4   // HX711 Data Pin
#define HX711_SCK   5   // HX711 Clock Pin

// ============================================================
// GLOBAL VARIABLES
// ============================================================

HX711 scale;

// Calibration values
long zero_offset = 0;
float calibration_factor = 0;

// Moving average filter
const int MOVING_AVG_SIZE = 10;
float weight_history[MOVING_AVG_SIZE] = {0};
int history_index = 0;
bool history_filled = false;

// Tracking
float last_sent_weight_kg = 0;
unsigned long last_post_ms = 0;
unsigned long last_wifi_retry_ms = 0;

// Status tracking
String current_status = "Unknown";
float current_gas_percentage = 0;

// ============================================================
// STATUS CALCULATION
// ============================================================

struct GasStatus {
    String status;
    float percentage;
};

GasStatus calculateGasStatus(float weightKg) {
    GasStatus result;
    
    // Calculate gas weight (total weight - empty cylinder weight)
    float gasWeight = weightKg - EMPTY_CYLINDER_WEIGHT_KG;
    float maxGasWeight = FULL_CYLINDER_WEIGHT_KG - EMPTY_CYLINDER_WEIGHT_KG;
    
    // Calculate percentage (0-100%)
    result.percentage = (gasWeight / maxGasWeight) * 100.0f;
    result.percentage = constrain(result.percentage, 0.0f, 100.0f);
    
    // Determine status based on actual weight
    // For 2kg load cell: 2kg=Full, 1kg=Half, <500g=Low
    if (weightKg >= 1.8) {
        result.status = "Full";
    } else if (weightKg >= 1.0) {
        result.status = "Good";
    } else if (weightKg >= 0.75) {
        result.status = "Half";
    } else if (weightKg >= 0.5) {
        result.status = "Low";
    } else {
        result.status = "Critical";
    }
    
    return result;
}

// ============================================================
// WIFI FUNCTIONS
// ============================================================

void connectToWiFi() {
    Serial.printf("\n[WiFi] Connecting to '%s'...\n", WIFI_SSID);
    
    #if LED_WIFI >= 0
    digitalWrite(LED_WIFI, LOW);
    #endif
    
    WiFi.mode(WIFI_STA);
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
    
    unsigned long startAttemptTime = millis();
    int dots = 0;
    
    while (WiFi.status() != WL_CONNECTED && millis() - startAttemptTime < 15000) {
        Serial.print('.');
        dots++;
        if (dots >= 50) {
            Serial.println();
            dots = 0;
        }
        
        #if LED_WIFI >= 0
        digitalWrite(LED_WIFI, !digitalRead(LED_WIFI)); // Blink while connecting
        #endif
        
        delay(300);
    }
    
    Serial.println();
    
    if (WiFi.status() == WL_CONNECTED) {
        Serial.println("[WiFi] Connected successfully!");
        Serial.print("[WiFi] IP Address: ");
        Serial.println(WiFi.localIP());
        Serial.print("[WiFi] Signal Strength: ");
        Serial.print(WiFi.RSSI());
        Serial.println(" dBm");
        
        #if LED_WIFI >= 0
        digitalWrite(LED_WIFI, HIGH); // Solid on when connected
        #endif
    } else {
        Serial.println("[WiFi] Connection failed! Will retry later.");
        
        #if LED_WIFI >= 0
        digitalWrite(LED_WIFI, LOW);
        #endif
    }
}

void ensureWiFi() {
    if (WiFi.status() != WL_CONNECTED) {
        if (millis() - last_wifi_retry_ms >= WIFI_RETRY_INTERVAL_MS) {
            last_wifi_retry_ms = millis();
            connectToWiFi();
        }
    }
}

// ============================================================
// API FUNCTIONS
// ============================================================

void postReading(float weightKg, int batteryLevel = -1) {
    ensureWiFi();
    
    if (WiFi.status() != WL_CONNECTED) {
        Serial.println("[API] Skipping POST - WiFi disconnected");
        return;
    }
    
    HTTPClient http;
    http.begin(API_ENDPOINT);
    http.addHeader("Content-Type", "application/json");
    http.setTimeout(10000); // 10 second timeout
    
    // Create JSON payload
    StaticJsonDocument<256> doc;
    doc["weight"] = round(weightKg * 1000) / 1000.0; // Round to 3 decimal places
    doc["deviceId"] = DEVICE_ID;
    
    if (batteryLevel >= 0) {
        doc["batteryLevel"] = batteryLevel;
    }
    
    String payload;
    serializeJson(doc, payload);
    
    Serial.println("[API] Sending reading...");
    Serial.print("[API] Endpoint: ");
    Serial.println(API_ENDPOINT);
    Serial.print("[API] Payload: ");
    Serial.println(payload);
    
    int httpCode = http.POST(payload);
    
    if (httpCode > 0) {
        Serial.printf("[API] Response code: %d\n", httpCode);
        
        if (httpCode == 200) {
            String response = http.getString();
            Serial.println("[API] Response: " + response);
            
            // Parse response to get status
            StaticJsonDocument<512> responseDoc;
            if (deserializeJson(responseDoc, response) == DeserializationError::Ok) {
                if (responseDoc["data"]["status"].is<const char*>()) {
                    current_status = responseDoc["data"]["status"].as<String>();
                }
                if (responseDoc["data"]["gasPercentage"].is<float>()) {
                    current_gas_percentage = responseDoc["data"]["gasPercentage"];
                }
            }
        }
    } else {
        Serial.printf("[API] POST failed, error: %s\n", http.errorToString(httpCode).c_str());
    }
    
    http.end();
}

bool shouldPost(float avgWeightKg) {
    // Always post at intervals
    if (millis() - last_post_ms < POST_INTERVAL_MS) {
        return false;
    }
    
    // Post if weight changed significantly
    if (fabs(avgWeightKg - last_sent_weight_kg) >= MIN_WEIGHT_DELTA_FOR_SEND) {
        return true;
    }
    
    // Also post periodically even if weight hasn't changed
    return true;
}

// ============================================================
// SCALE FUNCTIONS
// ============================================================

float getMovingAverage(float newValue) {
    weight_history[history_index] = newValue;
    history_index = (history_index + 1) % MOVING_AVG_SIZE;
    
    if (history_index == 0) {
        history_filled = true;
    }
    
    int count = history_filled ? MOVING_AVG_SIZE : history_index;
    if (count == 0) return newValue;
    
    float sum = 0;
    for (int i = 0; i < count; i++) {
        sum += weight_history[i];
    }
    
    return sum / count;
}

void performTare() {
    Serial.println("\n[Scale] Performing tare (zeroing)...");
    Serial.println("[Scale] Please remove all weight from the scale.");
    delay(2000);
    
    zero_offset = 0;
    for (int i = 0; i < 10; i++) {
        zero_offset += scale.read();
        delay(100);
    }
    zero_offset /= 10;
    
    Serial.print("[Scale] Zero offset set to: ");
    Serial.println(zero_offset);
}

void printCalibrationInstructions() {
    Serial.println("\n============================================");
    Serial.println("        CALIBRATION INSTRUCTIONS");
    Serial.println("============================================");
    Serial.println("1. Place a known weight on the scale");
    Serial.println("2. Type the weight in GRAMS in Serial Monitor");
    Serial.println("3. Press Enter to set calibration factor");
    Serial.println("Example: Type '500' for 500 grams");
    Serial.println("============================================\n");
}

// ============================================================
// DISPLAY FUNCTIONS
// ============================================================

void displayReading(float rawWeight, float avgWeightKg, GasStatus status) {
    Serial.println("--------------------------------------------");
    Serial.printf("Raw Weight: %.2f g | Avg: %.3f kg\n", rawWeight, avgWeightKg);
    Serial.printf("Gas Level: %.1f%% | Status: %s\n", status.percentage, status.status.c_str());
    
    // Visual gas meter
    Serial.print("Gas Meter: [");
    int filled = (int)(status.percentage / 5); // 20 segments
    for (int i = 0; i < 20; i++) {
        if (i < filled) Serial.print("█");
        else Serial.print("░");
    }
    Serial.println("]");
    
    // Alerts
    if (status.percentage < 25) {
        Serial.println("⚠️  WARNING: Low gas level! Consider ordering a refill.");
    }
    if (status.percentage < 10) {
        Serial.println("🚨 CRITICAL: Very low gas! Auto-booking may trigger.");
    }
}

// ============================================================
// SETUP
// ============================================================

void setup() {
    Serial.begin(115200);
    delay(1000);
    
    // Initialize LED pins
    #if LED_WIFI >= 0
    pinMode(LED_WIFI, OUTPUT);
    digitalWrite(LED_WIFI, LOW);
    #endif
    
    #if LED_STATUS >= 0
    pinMode(LED_STATUS, OUTPUT);
    digitalWrite(LED_STATUS, LOW);
    #endif
    
    Serial.println("\n============================================");
    Serial.println("  ServConnect Gas Cylinder Monitor v2.0");
    Serial.println("============================================");
    Serial.printf("Device ID: %s\n", DEVICE_ID);
    Serial.printf("API Endpoint: %s\n", API_ENDPOINT);
    Serial.printf("Post Interval: %lu ms\n", POST_INTERVAL_MS);
    Serial.printf("Full Cylinder: %.2f kg\n", FULL_CYLINDER_WEIGHT_KG);
    Serial.printf("Empty Cylinder: %.2f kg\n", EMPTY_CYLINDER_WEIGHT_KG);
    Serial.println("============================================\n");
    
    // Initialize HX711
    scale.begin(HX711_DT, HX711_SCK);
    Serial.println("[Scale] HX711 initialized");
    
    // Connect to WiFi
    connectToWiFi();
    
    // Perform tare
    performTare();
    
    // Print calibration instructions
    printCalibrationInstructions();
}

// ============================================================
// MAIN LOOP
// ============================================================

void loop() {
    // Read raw value from scale
    long raw = scale.read();
    
    // Correct sign based on wiring (may need adjustment)
    long corrected = -raw;
    
    // Net reading after zero offset
    long net = corrected - (-zero_offset);
    
    // Handle calibration input from Serial
    if (Serial.available() > 0) {
        float known_weight = Serial.parseFloat();
        
        if (known_weight >= MIN_CALIBRATION_WEIGHT_GRAM) {
            calibration_factor = (float)net / known_weight;
            Serial.printf("\n[Scale] Calibration factor set to: %.4f\n", calibration_factor);
            Serial.println("[Scale] Calibration complete! You can now measure weights.\n");
        } else if (known_weight > 0) {
            Serial.println("[Scale] Weight too small for calibration. Use at least 50g.");
        }
        
        // Clear serial buffer
        while (Serial.available()) Serial.read();
    }
    
    // Calculate and display weight if calibrated
    if (calibration_factor != 0) {
        float weight_g = (float)net / calibration_factor;
        float avg_weight_g = getMovingAverage(weight_g);
        float avg_weight_kg = avg_weight_g / 1000.0f;
        
        // Ensure non-negative
        if (avg_weight_kg < 0) avg_weight_kg = 0;
        
        // Calculate status
        GasStatus status = calculateGasStatus(avg_weight_kg);
        
        // Display reading every loop
        displayReading(weight_g, avg_weight_kg, status);
        
        // Post to API if needed
        if (shouldPost(avg_weight_kg)) {
            postReading(avg_weight_kg);
            last_post_ms = millis();
            last_sent_weight_kg = avg_weight_kg;
        }
        
        // Update status LED
        #if LED_STATUS >= 0
        if (status.percentage < 25) {
            // Blink for low gas
            digitalWrite(LED_STATUS, (millis() / 500) % 2);
        } else {
            digitalWrite(LED_STATUS, HIGH);
        }
        #endif
        
    } else {
        // Not calibrated yet
        Serial.println("[Scale] Awaiting calibration... Enter known weight in grams.");
        
        // Show raw reading for debugging
        Serial.printf("[Scale] Raw: %ld, Net: %ld\n", raw, net);
        
        // For development: post uncalibrated readings
        if (millis() - last_post_ms > POST_INTERVAL_MS * 2) {
            float approx_weight = abs(net) / 500000.0f; // Very rough approximation
            Serial.printf("[Dev] Sending uncalibrated reading: %.3f kg\n", approx_weight);
            postReading(approx_weight);
            last_post_ms = millis();
        }
    }
    
    delay(500); // Read every 500ms
}
