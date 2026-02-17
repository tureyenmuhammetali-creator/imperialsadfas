/**
 * IMPERIAL VIP - TURBO MODE
 * Ultra-aggressive instant navigation
 * SIFIR gecikme garantisi!
 */

(function() {
    'use strict';

    console.log('🔥 TURBO MODE: INITIALIZING...');

    // ============ CONFIG ============
    const PRELOAD_ALL_LINKS = true;
    const INSTANT_CLICK = true;
    const USE_FETCH_API = true;

    // ============ STATE ============
    const cache = new Map();
    const prefetching = new Set();
    let isNavigating = false;

    // ============ 1. INSTANT PRELOAD ALL NAVBAR (Aggressive!) ============
    function preloadAllNavbar() {
        const navLinks = document.querySelectorAll('nav a, .nav-link, .navbar a');
        
        console.log(`🔥 Preloading ${navLinks.length} navbar links...`);
        
        navLinks.forEach((link, index) => {
            const href = link.getAttribute('href');
            if (href && href.startsWith('/') && !href.startsWith('/#')) {
                // Immediate prefetch (no delay!)
                setTimeout(() => {
                    instantPrefetch(href);
                }, index * 20); // Stagger by 20ms only
            }
        });
    }

    // ============ 2. FETCH API PREFETCH (Faster than link prefetch!) ============
    function instantPrefetch(url) {
        // Already cached
        if (cache.has(url) || prefetching.has(url)) {
            return;
        }

        // Same page
        if (url === window.location.pathname) {
            return;
        }

        prefetching.add(url);

        // Use Fetch API for instant prefetch
        fetch(url, {
            method: 'GET',
            credentials: 'include',
            cache: 'force-cache',
            priority: 'high'
        })
        .then(response => response.text())
        .then(html => {
            cache.set(url, html);
            prefetching.delete(url);
            console.log(`⚡ CACHED: ${url}`);
        })
        .catch(err => {
            prefetching.delete(url);
            console.warn(`Failed to prefetch ${url}:`, err);
        });

        // Also add link prefetch as backup
        const link = document.createElement('link');
        link.rel = 'prefetch';
        link.href = url;
        document.head.appendChild(link);
    }

    // ============ 3. MOUSEDOWN INSTANT LOAD ============
    document.addEventListener('mousedown', function(e) {
        if (e.button !== 0) return; // Only left click
        
        const link = e.target.closest('a');
        if (!link || !link.href || isNavigating) return;

        const href = link.getAttribute('href');
        if (!href || !href.startsWith('/') || href.startsWith('/#')) return;

        // Prevent multiple navigations
        isNavigating = true;

        // Visual feedback
        document.body.style.cursor = 'wait';
        link.style.opacity = '0.7';

        // If cached, instant load!
        if (cache.has(href)) {
            console.log(`🚀 INSTANT LOAD from cache: ${href}`);
            // Let the browser handle the navigation
            // Cache will make it instant
        } else {
            // Not cached, prefetch NOW
            console.log(`⚡ On-demand prefetch: ${href}`);
            instantPrefetch(href);
        }

        // Reset after navigation
        setTimeout(() => {
            isNavigating = false;
            document.body.style.cursor = '';
        }, 100);
    }, { passive: true });

    // ============ 4. HOVER PREFETCH (Even faster!) ============
    let hoverTimeout;
    document.addEventListener('mouseover', function(e) {
        const link = e.target.closest('a');
        if (!link || !link.href) return;

        const href = link.getAttribute('href');
        if (!href || !href.startsWith('/') || href.startsWith('/#')) return;

        clearTimeout(hoverTimeout);
        
        // INSTANT prefetch on hover (no delay!)
        hoverTimeout = setTimeout(() => {
            instantPrefetch(href);
        }, 0); // ZERO delay!
    }, { passive: true });

    // ============ 5. TOUCH INSTANT PREFETCH ============
    document.addEventListener('touchstart', function(e) {
        const link = e.target.closest('a');
        if (!link || !link.href) return;

        const href = link.getAttribute('href');
        if (href && href.startsWith('/') && !href.startsWith('/#')) {
            instantPrefetch(href);
        }
    }, { passive: true });

    // ============ 6. PRELOAD ON IDLE ============
    function preloadOnIdle() {
        if ('requestIdleCallback' in window) {
            requestIdleCallback(() => {
                preloadAllNavbar();
                
                // Also preload all visible links
                const allLinks = document.querySelectorAll('a[href^="/"]');
                allLinks.forEach((link, index) => {
                    setTimeout(() => {
                        const href = link.getAttribute('href');
                        if (href && !href.startsWith('/#')) {
                            instantPrefetch(href);
                        }
                    }, index * 50);
                });
            }, { timeout: 2000 });
        } else {
            setTimeout(preloadAllNavbar, 500);
        }
    }

    // ============ 7. DNS PREFETCH ============
    function setupDNSPrefetch() {
        const currentDomain = window.location.origin;
        const link = document.createElement('link');
        link.rel = 'dns-prefetch';
        link.href = currentDomain;
        document.head.appendChild(link);
    }

    // ============ 8. PRECONNECT ============
    function setupPreconnect() {
        const link = document.createElement('link');
        link.rel = 'preconnect';
        link.href = window.location.origin;
        document.head.appendChild(link);
    }

    // ============ 9. SERVICE WORKER (Advanced caching) ============
    function setupServiceWorker() {
        if ('serviceWorker' in navigator) {
            // Service worker for offline support and caching
            // Uncomment when ready for production
            /*
            navigator.serviceWorker.register('/sw.js')
                .then(reg => console.log('Service Worker registered'))
                .catch(err => console.log('Service Worker registration failed'));
            */
        }
    }

    // ============ 10. PERFORMANCE OBSERVER ============
    function setupPerformanceObserver() {
        if ('PerformanceObserver' in window) {
            try {
                // Measure navigation timing
                const observer = new PerformanceObserver((list) => {
                    list.getEntries().forEach((entry) => {
                        if (entry.entryType === 'navigation') {
                            const loadTime = entry.loadEventEnd - entry.fetchStart;
                            console.log(`⏱️ Page load: ${Math.round(loadTime)}ms`);
                            
                            if (loadTime < 200) {
                                console.log('🚀 ULTRA FAST! <200ms');
                            } else if (loadTime < 500) {
                                console.log('⚡ FAST! <500ms');
                            }
                        }
                    });
                });
                
                observer.observe({ entryTypes: ['navigation'] });
            } catch (e) {
                // Ignore errors
            }
        }
    }

    // ============ 11. INSTANT CSS TRANSITIONS ============
    function setupInstantTransitions() {
        const style = document.createElement('style');
        style.textContent = `
            /* TURBO MODE: Instant transitions */
            * {
                transition-duration: 0.1s !important;
            }
            
            .nav-link {
                transition: all 0.05s ease !important;
            }
            
            .nav-link:hover {
                transform: translateY(-1px);
            }
            
            .nav-link:active {
                transform: translateY(0);
                opacity: 0.7;
            }
        `;
        document.head.appendChild(style);
    }

    // ============ 12. DISABLE UNNECESSARY ANIMATIONS ============
    function optimizeAnimations() {
        // Disable slow animations on navbar
        document.querySelectorAll('.nav-link, nav a').forEach(link => {
            link.style.transitionDuration = '0.1s';
        });
    }

    // ============ INITIALIZATION ============
    function init() {
        console.log('🔥 TURBO MODE: ACTIVE!');
        
        setupDNSPrefetch();
        setupPreconnect();
        setupPerformanceObserver();
        setupInstantTransitions();
        
        // Preload navbar immediately on page load
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => {
                setTimeout(() => {
                    preloadAllNavbar();
                    optimizeAnimations();
                }, 100);
            });
        } else {
            setTimeout(() => {
                preloadAllNavbar();
                optimizeAnimations();
            }, 100);
        }
        
        // Preload all links when idle
        preloadOnIdle();
        
        console.log('⚡ All navbar links will be preloaded in background');
        console.log('⚡ Click = INSTANT navigation!');
    }

    // Start immediately
    init();

    // Export for debugging
    window.turboMode = {
        cache: cache,
        prefetch: instantPrefetch,
        stats: () => {
            console.log(`📊 Cached pages: ${cache.size}`);
            console.log(`📊 Prefetching: ${prefetching.size}`);
            console.log('Cached URLs:', Array.from(cache.keys()));
        }
    };

})();

