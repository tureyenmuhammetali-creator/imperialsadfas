/**
 * Imperial VIP - Performance Boost Script
 * Ultra-optimized for maximum speed
 */

(function() {
    'use strict';

    // ============ 1. LAZY LOADING POLYFILL ============
    if ('loading' in HTMLImageElement.prototype === false) {
        const images = document.querySelectorAll('img[loading="lazy"]');
        const imageObserver = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const img = entry.target;
                    img.src = img.dataset.src || img.src;
                    imageObserver.unobserve(img);
                }
            });
        }, {
            rootMargin: '50px 0px',
            threshold: 0.01
        });

        images.forEach(img => imageObserver.observe(img));
    }

    // ============ 2. PRELOAD NEXT PAGE (Link Prefetch) ============
    let linkPrefetchTimeout;
    document.addEventListener('mouseover', function(e) {
        const link = e.target.closest('a');
        if (link && link.href && link.host === window.location.host) {
            clearTimeout(linkPrefetchTimeout);
            linkPrefetchTimeout = setTimeout(() => {
                const prefetchLink = document.createElement('link');
                prefetchLink.rel = 'prefetch';
                prefetchLink.href = link.href;
                document.head.appendChild(prefetchLink);
            }, 100);
        }
    }, { passive: true });

    // ============ 3. IMAGE ERROR HANDLING ============
    document.addEventListener('error', function(e) {
        if (e.target.tagName === 'IMG') {
            e.target.style.display = 'none';
            console.warn('Image failed to load:', e.target.src);
        }
    }, true);

    // ============ 4. REDUCE LAYOUT SHIFTS (CLS) ============
    const images = document.querySelectorAll('img:not([width]):not([height])');
    images.forEach(img => {
        if (img.naturalWidth > 0) {
            const aspectRatio = img.naturalHeight / img.naturalWidth;
            img.style.aspectRatio = `${img.naturalWidth} / ${img.naturalHeight}`;
        }
    });

    // ============ 5. DEBOUNCED SCROLL HANDLER ============
    let scrollTimeout;
    let lastScrollY = window.scrollY;
    
    window.addEventListener('scroll', function() {
        if (scrollTimeout) return;
        
        scrollTimeout = setTimeout(() => {
            const currentScrollY = window.scrollY;
            
            // Scroll direction detection
            if (currentScrollY > lastScrollY && currentScrollY > 100) {
                document.body.classList.add('scrolled-down');
                document.body.classList.remove('scrolled-up');
            } else {
                document.body.classList.add('scrolled-up');
                document.body.classList.remove('scrolled-down');
            }
            
            lastScrollY = currentScrollY;
            scrollTimeout = null;
        }, 100);
    }, { passive: true });

    // ============ 6. PREFETCH CRITICAL RESOURCES ============
    function prefetchCriticalResources() {
        const criticalPages = [
            '/Reservation/Index',
            '/Vehicle/Index',
            '/Home/AllRegions'
        ];

        setTimeout(() => {
            criticalPages.forEach(page => {
                const link = document.createElement('link');
                link.rel = 'prefetch';
                link.href = page;
                document.head.appendChild(link);
            });
        }, 3000); // 3 saniye sonra prefetch başlat
    }

    if ('requestIdleCallback' in window) {
        requestIdleCallback(prefetchCriticalResources, { timeout: 5000 });
    } else {
        setTimeout(prefetchCriticalResources, 3000);
    }

    // ============ 7. WEB VITALS MONITORING (Optional) ============
    function reportWebVitals() {
        if ('PerformanceObserver' in window) {
            // Largest Contentful Paint (LCP)
            new PerformanceObserver((list) => {
                const entries = list.getEntries();
                const lastEntry = entries[entries.length - 1];
                console.log('LCP:', lastEntry.renderTime || lastEntry.loadTime);
            }).observe({ entryTypes: ['largest-contentful-paint'] });

            // First Input Delay (FID)
            new PerformanceObserver((list) => {
                const entries = list.getEntries();
                entries.forEach(entry => {
                    console.log('FID:', entry.processingStart - entry.startTime);
                });
            }).observe({ entryTypes: ['first-input'] });

            // Cumulative Layout Shift (CLS)
            let clsScore = 0;
            new PerformanceObserver((list) => {
                list.getEntries().forEach(entry => {
                    if (!entry.hadRecentInput) {
                        clsScore += entry.value;
                    }
                });
                console.log('CLS:', clsScore);
            }).observe({ entryTypes: ['layout-shift'] });
        }
    }

    // Enable Web Vitals monitoring (Development only)
    if (window.location.hostname === 'localhost') {
        reportWebVitals();
    }

    // ============ 8. CONNECTION TYPE OPTIMIZATION ============
    if ('connection' in navigator) {
        const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
        
        if (connection) {
            // Slow connection - reduce image quality
            if (connection.effectiveType === 'slow-2g' || connection.effectiveType === '2g') {
                document.body.classList.add('slow-connection');
                console.log('Slow connection detected. Optimizing...');
            }
            
            // Save data mode
            if (connection.saveData) {
                document.body.classList.add('save-data-mode');
                console.log('Data saver mode enabled.');
            }
        }
    }

    // ============ 9. MEMORY MANAGEMENT ============
    // Clear unused images from memory when scrolled far
    let memoryCleanupTimeout;
    window.addEventListener('scroll', function() {
        clearTimeout(memoryCleanupTimeout);
        memoryCleanupTimeout = setTimeout(() => {
            const images = document.querySelectorAll('img[data-loaded="true"]');
            images.forEach(img => {
                const rect = img.getBoundingClientRect();
                if (rect.top > window.innerHeight * 3 || rect.bottom < -window.innerHeight * 3) {
                    // Image is far from viewport, can be unloaded
                    if (img.dataset.src) {
                        img.src = img.dataset.placeholder || '';
                        img.removeAttribute('data-loaded');
                    }
                }
            });
        }, 1000);
    }, { passive: true });

    // ============ 10. CRITICAL RENDERING PATH ============
    // Force browser to process critical content first
    if ('requestAnimationFrame' in window) {
        requestAnimationFrame(() => {
            // Add 'loaded' class to trigger CSS animations
            document.body.classList.add('loaded');
        });
    }

    console.log('⚡ Imperial VIP Performance Boost: ACTIVE');
})();

