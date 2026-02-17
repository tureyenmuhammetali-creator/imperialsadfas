/**
 * IMPERIAL VIP - Reservation System
 * Distance calculation and booking management
 */

document.addEventListener('DOMContentLoaded', function() {
    initReservationForm();
});

// Istanbul locations database with coordinates
const locations = {
    airports: [
        { id: 'ist', name: 'İstanbul Havalimanı (IST)', lat: 41.2608, lng: 28.7418 },
        { id: 'saw', name: 'Sabiha Gökçen Havalimanı (SAW)', lat: 40.8986, lng: 29.3092 }
    ],
    hotels: [
        { id: 'four-seasons-bosphorus', name: 'Four Seasons Hotel Bosphorus', lat: 41.0486, lng: 29.0336 },
        { id: 'ciragan-palace', name: 'Çırağan Palace Kempinski', lat: 41.0445, lng: 29.0244 },
        { id: 'raffles-istanbul', name: 'Raffles Istanbul', lat: 41.0573, lng: 29.0107 },
        { id: 'st-regis', name: 'The St. Regis Istanbul', lat: 41.0554, lng: 29.0134 },
        { id: 'shangri-la', name: 'Shangri-La Bosphorus', lat: 41.0389, lng: 29.0256 },
        { id: 'ritz-carlton', name: 'The Ritz-Carlton Istanbul', lat: 41.0452, lng: 29.0134 },
        { id: 'conrad', name: 'Conrad Istanbul Bosphorus', lat: 41.0398, lng: 29.0067 },
        { id: 'swissotel', name: 'Swissotel The Bosphorus', lat: 41.0467, lng: 29.0134 },
        { id: 'hilton-bosphorus', name: 'Hilton Istanbul Bosphorus', lat: 41.0489, lng: 29.0134 },
        { id: 'w-istanbul', name: 'W Istanbul', lat: 41.0356, lng: 28.9834 }
    ],
    popularAreas: [
        { id: 'taksim', name: 'Taksim Meydanı', lat: 41.0370, lng: 28.9850 },
        { id: 'sultanahmet', name: 'Sultanahmet', lat: 41.0054, lng: 28.9768 },
        { id: 'besiktas', name: 'Beşiktaş', lat: 41.0422, lng: 29.0075 },
        { id: 'kadikoy', name: 'Kadıköy', lat: 40.9927, lng: 29.0229 },
        { id: 'nisantasi', name: 'Nişantaşı', lat: 41.0481, lng: 28.9942 },
        { id: 'levent', name: 'Levent', lat: 41.0819, lng: 29.0106 },
        { id: 'maslak', name: 'Maslak', lat: 41.1089, lng: 29.0206 },
        { id: 'ortakoy', name: 'Ortaköy', lat: 41.0479, lng: 29.0267 }
    ]
};

/**
 * Initialize reservation form
 */
function initReservationForm() {
    const form = document.getElementById('reservationForm');
    if (!form) return;

    // Initialize location selects
    populateLocationSelects();

    // Form event listeners
    const pickupType = document.getElementById('pickupType');
    const dropoffType = document.getElementById('dropoffType');
    const pickupLocation = document.getElementById('pickupLocation');
    const dropoffLocation = document.getElementById('dropoffLocation');

    if (pickupType) {
        pickupType.addEventListener('change', () => updateLocationOptions('pickup'));
    }
    if (dropoffType) {
        dropoffType.addEventListener('change', () => updateLocationOptions('dropoff'));
    }
    if (pickupLocation) {
        pickupLocation.addEventListener('change', calculateDistance);
    }
    if (dropoffLocation) {
        dropoffLocation.addEventListener('change', calculateDistance);
    }

    // Form submission
    form.addEventListener('submit', handleReservation);

    // Initialize with default options
    updateLocationOptions('pickup');
    updateLocationOptions('dropoff');
}

/**
 * Populate location selects with data
 */
function populateLocationSelects() {
    // This function sets up initial states
    // Actual options are populated by updateLocationOptions
}

/**
 * Update location options based on type selection
 */
function updateLocationOptions(type) {
    const typeSelect = document.getElementById(`${type}Type`);
    const locationSelect = document.getElementById(`${type}Location`);
    const customAddressGroup = document.getElementById(`${type}CustomAddress`);

    if (!typeSelect || !locationSelect) return;

    const selectedType = typeSelect.value;
    locationSelect.innerHTML = '<option value="">Seçiniz...</option>';

    // Show/hide custom address field
    if (customAddressGroup) {
        customAddressGroup.style.display = selectedType === 'address' ? 'block' : 'none';
    }

    let optionsData = [];

    switch (selectedType) {
        case 'airport':
            optionsData = locations.airports;
            break;
        case 'hotel':
            optionsData = locations.hotels;
            break;
        case 'address':
            optionsData = locations.popularAreas;
            locationSelect.innerHTML = '<option value="">Popüler bölgeler...</option>';
            break;
        default:
            return;
    }

    optionsData.forEach(location => {
        const option = document.createElement('option');
        option.value = JSON.stringify({ id: location.id, lat: location.lat, lng: location.lng });
        option.textContent = location.name;
        locationSelect.appendChild(option);
    });

    calculateDistance();
}

/**
 * Calculate distance between pickup and dropoff
 */
function calculateDistance() {
    const pickupLocation = document.getElementById('pickupLocation');
    const dropoffLocation = document.getElementById('dropoffLocation');
    const distanceDisplay = document.getElementById('distanceDisplay');
    const distanceValue = document.getElementById('distanceValue');
    const estimatedPrice = document.getElementById('estimatedPrice');

    if (!pickupLocation?.value || !dropoffLocation?.value) {
        if (distanceDisplay) distanceDisplay.classList.remove('visible');
        return;
    }

    try {
        const pickup = JSON.parse(pickupLocation.value);
        const dropoff = JSON.parse(dropoffLocation.value);

        // Calculate distance using Haversine formula
        const distance = haversineDistance(pickup.lat, pickup.lng, dropoff.lat, dropoff.lng);
        
        // Round to 1 decimal place
        const roundedDistance = Math.round(distance * 10) / 10;

        if (distanceValue) {
            distanceValue.innerHTML = `${roundedDistance} <span>KM</span>`;
        }

        // Calculate estimated price (example pricing)
        if (estimatedPrice) {
            const basePrice = 500; // Base fare in TL
            const pricePerKm = 15; // Price per km in TL
            const totalPrice = Math.round(basePrice + (roundedDistance * pricePerKm));
            estimatedPrice.innerHTML = `₺${totalPrice.toLocaleString('tr-TR')}`;
        }

        if (distanceDisplay) {
            distanceDisplay.classList.add('visible');
        }

    } catch (e) {
        console.error('Distance calculation error:', e);
        if (distanceDisplay) distanceDisplay.classList.remove('visible');
    }
}

/**
 * Haversine formula to calculate distance between two coordinates
 */
function haversineDistance(lat1, lng1, lat2, lng2) {
    const R = 6371; // Earth's radius in kilometers
    const dLat = toRad(lat2 - lat1);
    const dLng = toRad(lng2 - lng1);
    
    const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
              Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) *
              Math.sin(dLng / 2) * Math.sin(dLng / 2);
    
    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    
    // Multiply by 1.3 to account for road distance (not direct line)
    return R * c * 1.3;
}

function toRad(deg) {
    return deg * (Math.PI / 180);
}

/**
 * Handle reservation form submission
 */
function handleReservation(e) {
    e.preventDefault();

    const form = e.target;
    const formData = new FormData(form);
    const submitBtn = form.querySelector('.form-submit');

    // Validate required fields
    const required = ['pickupType', 'pickupLocation', 'dropoffType', 'dropoffLocation', 'date', 'time', 'passengers', 'name', 'phone'];
    let isValid = true;

    required.forEach(field => {
        const input = form.querySelector(`[name="${field}"]`) || document.getElementById(field);
        if (input && !input.value) {
            isValid = false;
            input.classList.add('error');
        } else if (input) {
            input.classList.remove('error');
        }
    });

    if (!isValid) {
        window.ImperialVIP?.showNotification('Lütfen tüm zorunlu alanları doldurun.', 'error');
        return;
    }

    // Show loading state
    if (submitBtn) {
        submitBtn.classList.add('loading');
        submitBtn.disabled = true;
        submitBtn.textContent = 'İşleniyor...';
    }

    // Collect reservation data
    const reservationData = {
        pickup: {
            type: document.getElementById('pickupType')?.value,
            location: document.getElementById('pickupLocation')?.value,
            customAddress: document.getElementById('pickupAddress')?.value
        },
        dropoff: {
            type: document.getElementById('dropoffType')?.value,
            location: document.getElementById('dropoffLocation')?.value,
            customAddress: document.getElementById('dropoffAddress')?.value
        },
        date: document.getElementById('date')?.value,
        time: document.getElementById('time')?.value,
        passengers: document.getElementById('passengers')?.value,
        vehicleType: document.getElementById('vehicleType')?.value,
        customer: {
            name: document.getElementById('name')?.value,
            phone: document.getElementById('phone')?.value,
            email: document.getElementById('email')?.value
        },
        notes: document.getElementById('notes')?.value
    };

    // Simulate API call
    setTimeout(() => {
        console.log('Reservation Data:', reservationData);
        
        // Reset button
        if (submitBtn) {
            submitBtn.classList.remove('loading');
            submitBtn.disabled = false;
            submitBtn.textContent = 'Rezervasyon Yap';
        }

        // Show success message
        window.ImperialVIP?.showNotification('Rezervasyonunuz başarıyla alındı! En kısa sürede sizinle iletişime geçeceğiz.', 'success');

        // Reset form
        form.reset();
        document.getElementById('distanceDisplay')?.classList.remove('visible');
        updateLocationOptions('pickup');
        updateLocationOptions('dropoff');

    }, 1500);
}

/**
 * Quick booking from homepage
 */
function quickBook(type) {
    // Redirect to reservation page with pre-selected type
    window.location.href = `reservation.html?type=${type}`;
}

// Handle URL parameters for quick booking
const urlParams = new URLSearchParams(window.location.search);
const bookingType = urlParams.get('type');
if (bookingType) {
    const pickupType = document.getElementById('pickupType');
    if (pickupType && bookingType === 'airport') {
        pickupType.value = 'airport';
        updateLocationOptions('pickup');
    }
}

// Export for external use
window.ReservationSystem = {
    calculateDistance,
    quickBook,
    locations
};
