/* ========================================
   IMPERIAL VIP - Main JavaScript
======================================== */

document.addEventListener('DOMContentLoaded', function() {
    
    // ===== Mobile Navigation =====
    const navToggle = document.getElementById('nav-toggle');
    const navClose = document.getElementById('nav-close');
    const navMenu = document.getElementById('nav-menu');

    if (navToggle) {
        navToggle.addEventListener('click', function() {
            navMenu.classList.add('show');
            document.body.style.overflow = 'hidden';
        });
    }

    if (navClose) {
        navClose.addEventListener('click', function() {
            navMenu.classList.remove('show');
            document.body.style.overflow = '';
        });
    }

    // Close menu on link click
    const navLinks = document.querySelectorAll('.nav-link');
    navLinks.forEach(link => {
        link.addEventListener('click', function() {
            navMenu.classList.remove('show');
            document.body.style.overflow = '';
        });
    });

    // ===== Header Scroll Effect =====
    const header = document.getElementById('header');
    
    function handleScroll() {
        if (window.scrollY > 50) {
            header.classList.add('scrolled');
        } else {
            header.classList.remove('scrolled');
        }
    }

    window.addEventListener('scroll', handleScroll);
    handleScroll(); // Initial check

    // ===== Smooth Scroll =====
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function(e) {
            const href = this.getAttribute('href');
            // Only handle anchors that still start with # and are valid selectors
            if (href && href !== '#' && href.startsWith('#') && href.length > 1) {
                try {
                    const target = document.querySelector(href);
                    if (target) {
                        e.preventDefault();
                        target.scrollIntoView({
                            behavior: 'smooth',
                            block: 'start'
                        });
                    }
                } catch (err) {
                    // Invalid selector, let the browser handle it normally
                    console.log('Smooth scroll skipped for:', href);
                }
            }
        });
    });

    // ===== Form Validation Styling =====
    const formControls = document.querySelectorAll('.form-control');
    
    formControls.forEach(control => {
        control.addEventListener('blur', function() {
            if (this.value.trim() !== '') {
                this.classList.add('has-value');
            } else {
                this.classList.remove('has-value');
            }
        });

        // Check on load
        if (control.value.trim() !== '') {
            control.classList.add('has-value');
        }
    });

    // ===== Phone Input Formatting =====
    const phoneInputs = document.querySelectorAll('input[type="tel"], input[name*="Phone"], input[name*="phone"]');
    
    phoneInputs.forEach(input => {
        input.addEventListener('input', function(e) {
            let value = e.target.value.replace(/\D/g, '');
            // Ülke kodu 90 ile başlarsa kaldır, sadece 5XX XXX XX XX formatında göster
            if (value.startsWith('90') && value.length > 2) {
                value = value.substring(2);
            }
            
            if (value.length > 0) {
                if (value.length <= 3) {
                    value = value;
                } else if (value.length <= 6) {
                    value = value.substring(0, 3) + ' ' + value.substring(3);
                } else if (value.length <= 8) {
                    value = value.substring(0, 3) + ' ' + value.substring(3, 6) + ' ' + value.substring(6);
                } else {
                    value = value.substring(0, 3) + ' ' + value.substring(3, 6) + ' ' + value.substring(6, 8) + ' ' + value.substring(8, 10);
                }
            }
            
            e.target.value = value;
        });
    });

    // ===== Location Type Change =====
    const pickupType = document.getElementById('pickupType');
    const dropoffType = document.getElementById('dropoffType');
    const pickupLocation = document.getElementById('pickupLocation');
    const dropoffLocation = document.getElementById('dropoffLocation');

    const locationPlaceholders = {
        '0': 'Örn: İstanbul Havalimanı, Sabiha Gökçen',
        '1': 'Örn: Hilton Istanbul Bomonti',
        '2': 'Örn: Kadıköy, Moda Caddesi No:15'
    };

    if (pickupType && pickupLocation) {
        pickupType.addEventListener('change', function() {
            pickupLocation.placeholder = locationPlaceholders[this.value] || 'Konum giriniz';
        });
    }

    if (dropoffType && dropoffLocation) {
        dropoffType.addEventListener('change', function() {
            dropoffLocation.placeholder = locationPlaceholders[this.value] || 'Konum giriniz';
        });
    }

    // ===== Set Minimum Date for Date Inputs =====
    const dateInputs = document.querySelectorAll('input[type="date"]');
    const today = new Date().toISOString().split('T')[0];
    
    dateInputs.forEach(input => {
        if (!input.min) {
            input.min = today;
        }
    });

    // ===== Animation on Scroll (Simple) =====
    const animateElements = document.querySelectorAll('.feature-card, .service-card, .vehicle-card, .stat-item');
    
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver(function(entries) {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
            }
        });
    }, observerOptions);

    animateElements.forEach(el => {
        el.style.opacity = '0';
        el.style.transform = 'translateY(20px)';
        el.style.transition = 'opacity 0.5s ease, transform 0.5s ease';
        observer.observe(el);
    });

    // ===== Confirmation Dialogs =====
    const deleteButtons = document.querySelectorAll('[data-confirm]');
    
    deleteButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            const message = this.getAttribute('data-confirm') || 'Bu işlemi gerçekleştirmek istediğinize emin misiniz?';
            if (!confirm(message)) {
                e.preventDefault();
            }
        });
    });

    // ===== Close alerts =====
    const alerts = document.querySelectorAll('.alert');
    
    alerts.forEach(alert => {
        setTimeout(() => {
            alert.style.opacity = '0';
            alert.style.transform = 'translateY(-10px)';
            setTimeout(() => {
                alert.remove();
            }, 300);
        }, 3000);
    });

    // ===== Hero Slider - Professional Version =====
    const heroSlider = document.getElementById('heroSlider');
    if (heroSlider) {
        const slides = heroSlider.querySelectorAll('.hero-slide');
        const dots = document.querySelectorAll('.hero-dot');
        const prevBtn = document.getElementById('heroPrev');
        const nextBtn = document.getElementById('heroNext');
        const slideCounter = document.getElementById('slideCurrentNum');
        const SLIDE_DURATION = 5000; // 5 saniye (CSS animasyonu ile senkronize)
        
        if (slides.length > 1) {
            let currentIndex = 0;
            let autoSlideInterval;
            let isTransitioning = false;
            
            function updateSlideCounter(index) {
                if (slideCounter) {
                    slideCounter.textContent = String(index + 1).padStart(2, '0');
                }
            }
            
            function resetDotAnimations() {
                dots.forEach(dot => {
                    dot.classList.remove('active');
                    // Force reflow to restart animation
                    void dot.offsetWidth;
                });
            }
            
            function showSlide(index, direction = 'next') {
                if (isTransitioning) return;
                isTransitioning = true;
                
                // Handle wrap-around
                if (index >= slides.length) index = 0;
                if (index < 0) index = slides.length - 1;
                
                // Reset dot animations
                resetDotAnimations();
                
                // Update slides with smooth transition
                slides.forEach((slide, i) => {
                    slide.classList.remove('active');
                    if (i === index) {
                        // Small delay for better visual effect
                        setTimeout(() => {
                            slide.classList.add('active');
                        }, 50);
                    }
                });
                
                // Update dots
                setTimeout(() => {
                    dots[index].classList.add('active');
                }, 50);
                
                // Update counter
                updateSlideCounter(index);
                
                currentIndex = index;
                
                // Allow next transition after animation
                setTimeout(() => {
                    isTransitioning = false;
                }, 1200);
            }
            
            function nextSlide() {
                showSlide(currentIndex + 1, 'next');
            }
            
            function prevSlide() {
                showSlide(currentIndex - 1, 'prev');
            }
            
            function startAutoSlide() {
                stopAutoSlide();
                autoSlideInterval = setInterval(nextSlide, SLIDE_DURATION);
            }
            
            function stopAutoSlide() {
                if (autoSlideInterval) {
                    clearInterval(autoSlideInterval);
                    autoSlideInterval = null;
                }
            }
            
            // Event listeners
            if (nextBtn) {
                nextBtn.addEventListener('click', () => {
                    nextSlide();
                    stopAutoSlide();
                    startAutoSlide();
                });
            }
            
            if (prevBtn) {
                prevBtn.addEventListener('click', () => {
                    prevSlide();
                    stopAutoSlide();
                    startAutoSlide();
                });
            }
            
            dots.forEach((dot, index) => {
                dot.addEventListener('click', () => {
                    if (currentIndex !== index) {
                        showSlide(index);
                        stopAutoSlide();
                        startAutoSlide();
                    }
                });
            });
            
            // Keyboard navigation
            document.addEventListener('keydown', (e) => {
                if (e.key === 'ArrowRight') {
                    nextSlide();
                    stopAutoSlide();
                    startAutoSlide();
                } else if (e.key === 'ArrowLeft') {
                    prevSlide();
                    stopAutoSlide();
                    startAutoSlide();
                }
            });
            
            // Touch/Swipe support
            let touchStartX = 0;
            let touchEndX = 0;
            
            heroSlider.addEventListener('touchstart', (e) => {
                touchStartX = e.changedTouches[0].screenX;
            }, { passive: true });
            
            heroSlider.addEventListener('touchend', (e) => {
                touchEndX = e.changedTouches[0].screenX;
                const swipeThreshold = 50;
                
                if (touchStartX - touchEndX > swipeThreshold) {
                    nextSlide();
                    stopAutoSlide();
                    startAutoSlide();
                } else if (touchEndX - touchStartX > swipeThreshold) {
                    prevSlide();
                    stopAutoSlide();
                    startAutoSlide();
                }
            }, { passive: true });
            
            // Start auto slide
            startAutoSlide();
            
            // Pause on hover (desktop only)
            if (window.matchMedia('(hover: hover)').matches) {
                heroSlider.addEventListener('mouseenter', stopAutoSlide);
                heroSlider.addEventListener('mouseleave', startAutoSlide);
            }
            
            // Pause when tab is not visible
            document.addEventListener('visibilitychange', () => {
                if (document.hidden) {
                    stopAutoSlide();
                } else {
                    startAutoSlide();
                }
            });
        }
    }

    // ===== Reviews Carousel =====
    const reviewsCarousel = document.getElementById('reviewsCarousel');
    if (reviewsCarousel) {
        const reviewCards = reviewsCarousel.querySelectorAll('.review-card');
        const prevBtn = document.getElementById('reviewsPrev');
        const nextBtn = document.getElementById('reviewsNext');
        const dots = document.querySelectorAll('.reviews-dot');
        
        let currentReviewIndex = 0;
        const cardWidth = 374; // 350px card + 24px gap
        const visibleCards = window.innerWidth > 992 ? 3 : (window.innerWidth > 576 ? 2 : 1);
        const maxIndex = Math.max(0, reviewCards.length - visibleCards);
        
        function scrollToReview(index) {
            if (index < 0) index = 0;
            if (index > maxIndex) index = maxIndex;
            
            currentReviewIndex = index;
            reviewsCarousel.scrollTo({
                left: index * cardWidth,
                behavior: 'smooth'
            });
            
            // Update dots
            dots.forEach((dot, i) => {
                dot.classList.remove('active');
                if (i === index) {
                    dot.classList.add('active');
                }
            });
        }
        
        if (nextBtn) {
            nextBtn.addEventListener('click', () => {
                scrollToReview(currentReviewIndex + 1);
            });
        }
        
        if (prevBtn) {
            prevBtn.addEventListener('click', () => {
                scrollToReview(currentReviewIndex - 1);
            });
        }
        
        dots.forEach((dot, index) => {
            dot.addEventListener('click', () => {
                scrollToReview(index);
            });
        });
        
        // Auto scroll reviews
        let reviewsAutoScroll = setInterval(() => {
            if (currentReviewIndex >= maxIndex) {
                scrollToReview(0);
            } else {
                scrollToReview(currentReviewIndex + 1);
            }
        }, 5000);
        
        // Pause on hover
        reviewsCarousel.addEventListener('mouseenter', () => {
            clearInterval(reviewsAutoScroll);
        });
        
        reviewsCarousel.addEventListener('mouseleave', () => {
            reviewsAutoScroll = setInterval(() => {
                if (currentReviewIndex >= maxIndex) {
                    scrollToReview(0);
                } else {
                    scrollToReview(currentReviewIndex + 1);
                }
            }, 5000);
        });
    }

});

// ===== Add scrolled class style =====
const style = document.createElement('style');
style.textContent = `
    .header.scrolled {
        background: #ffffff;
        box-shadow: 0 2px 20px rgba(0, 0, 0, 0.1);
    }
    .header.scrolled .logo-text {
        color: #0a1628;
    }
    .header.scrolled .nav-link {
        color: #374151;
    }
    .header.scrolled .nav-link:hover {
        color: #c9a962;
    }
`;
document.head.appendChild(style);
