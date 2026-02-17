/**
 * Imperial VIP - INSTANT NAVIGATION
 * Ultra-fast page transitions - Zero delay!
 * Navbar'dan kategori değişikliği anında olacak
 */

(function() {
    'use strict';

    // ============ CONFIGURATION ============
    const config = {
        prefetchDelay: 50,          // Hover'dan 50ms sonra prefetch (çok hızlı!)
        mousedownPrefetch: true,    // Mouse down'da hemen prefetch
        touchstartPrefetch: true,   // Touch'da hemen prefetch
        preloadOnIdle: true,        // Boş zamanda tüm navbar linklerini yükle
        cacheEnabled: true,         // Browser cache kullan
        preloadDelay: 2000          // 2 saniye sonra tüm navbar'ı preload et
    };

    // ============ STATE ============
    let prefetchedUrls = new Set();
    let prefetchTimeout = null;
    let mousedownTriggered = false;

    // ============ 1. PREFETCH FUNCTION ============
    function prefetchUrl(url) {
        // Already prefetched
        if (prefetchedUrls.has(url)) {
            return;
        }

        // Same page, skip
        if (url === window.location.href || url === window.location.pathname) {
            return;
        }

        // External link, skip
        try {
            const urlObj = new URL(url, window.location.origin);
            if (urlObj.origin !== window.location.origin) {
                return;
            }
        } catch (e) {
            return;
        }

        // Create prefetch link
        const link = document.createElement('link');
        link.rel = 'prefetch';
        link.href = url;
        link.as = 'document';
        
        // Add to DOM
        document.head.appendChild(link);
        
        // Mark as prefetched
        prefetchedUrls.add(url);
        
        console.log('⚡ Prefetched:', url);
    }

    // ============ 2. PRERENDER FUNCTION (More aggressive) ============
    function prerenderUrl(url) {
        // Skip if already prefetched or same page
        if (prefetchedUrls.has(url) || url === window.location.pathname) {
            return;
        }

        // Create prerender link (Chrome will actually load the page!)
        const link = document.createElement('link');
        link.rel = 'prerender';
        link.href = url;
        
        document.head.appendChild(link);
        prefetchedUrls.add(url);
        
        console.log('🚀 Prerendered:', url);
    }

    // ============ 3. AGGRESSIVE NAVBAR PREFETCHING ============
    function prefetchNavbar() {
        const navLinks = document.querySelectorAll('.nav-link, .navbar a, nav a');
        
        navLinks.forEach((link, index) => {
            const href = link.getAttribute('href');
            if (href && href.startsWith('/')) {
                // Stagger prefetch to avoid overload (50ms apart)
                setTimeout(() => {
                    prefetchUrl(href);
                }, index * 50);
            }
        });
    }

    // ============ 4. HOVER PREFETCH (Fast!) ============
    document.addEventListener('mouseover', function(e) {
        const link = e.target.closest('a');
        if (!link || !link.href) return;

        // Clear any existing timeout
        clearTimeout(prefetchTimeout);

        // Set new timeout (very short delay)
        prefetchTimeout = setTimeout(() => {
            prefetchUrl(link.href);
        }, config.prefetchDelay);
    }, { passive: true });

    // ============ 5. MOUSEDOWN PREFETCH (Instant!) ============
    if (config.mousedownPrefetch) {
        document.addEventListener('mousedown', function(e) {
            const link = e.target.closest('a');
            if (!link || !link.href) return;

            mousedownTriggered = true;

            // Immediately prefetch AND prerender!
            clearTimeout(prefetchTimeout);
            prefetchUrl(link.href);
            
            // For navbar links, use prerender for instant load
            if (link.classList.contains('nav-link') || link.closest('nav')) {
                prerenderUrl(link.href);
            }
        }, { passive: true });
    }

    // ============ 6. TOUCHSTART PREFETCH (Mobile instant!) ============
    if (config.touchstartPrefetch) {
        document.addEventListener('touchstart', function(e) {
            const link = e.target.closest('a');
            if (!link || !link.href) return;

            // Immediately prefetch on touch
            prefetchUrl(link.href);
            
            // Navbar links get prerender treatment
            if (link.classList.contains('nav-link') || link.closest('nav')) {
                prerenderUrl(link.href);
            }
        }, { passive: true });
    }

    // ============ 7. INTERSECTION OBSERVER (Viewport prefetch) ============
    if ('IntersectionObserver' in window) {
        const linkObserver = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const link = entry.target;
                    const href = link.getAttribute('href');
                    if (href && href.startsWith('/')) {
                        prefetchUrl(href);
                        linkObserver.unobserve(link);
                    }
                }
            });
        }, {
            rootMargin: '100px' // Prefetch 100px before visible
        });

        // Observe all links (not just navbar)
        setTimeout(() => {
            document.querySelectorAll('a[href^="/"]').forEach(link => {
                linkObserver.observe(link);
            });
        }, 1000);
    }

    // ============ 8. IDLE TIME PREFETCHING ============
    if (config.preloadOnIdle) {
        function preloadOnIdle() {
            if ('requestIdleCallback' in window) {
                requestIdleCallback(() => {
                    prefetchNavbar();
                }, { timeout: 3000 });
            } else {
                setTimeout(prefetchNavbar, config.preloadDelay);
            }
        }

        // Start after page is fully loaded
        if (document.readyState === 'complete') {
            preloadOnIdle();
        } else {
            window.addEventListener('load', preloadOnIdle);
        }
    }

    // ============ 9. CACHE WARMING (Preload all navbar on load) ============
    window.addEventListener('load', function() {
        // Wait 1 second, then prefetch all navbar links
        setTimeout(() => {
            console.log('🔥 Warming cache: Prefetching all navbar links...');
            const navLinks = document.querySelectorAll('.nav-link, nav a');
            
            navLinks.forEach((link, index) => {
                const href = link.getAttribute('href');
                if (href && href.startsWith('/')) {
                    // Stagger by 100ms
                    setTimeout(() => {
                        prefetchUrl(href);
                    }, index * 100);
                }
            });
        }, 1000);
    });

    // ============ 10. BROWSER BACK/FORWARD CACHE ============
    // Ensure page is cached properly for instant back/forward
    window.addEventListener('pageshow', function(event) {
        if (event.persisted) {
            console.log('⚡ Page restored from bfcache (instant!)');
        }
    });

    // Don't prevent bfcache
    window.addEventListener('beforeunload', function() {
        // Don't do anything heavy here
    });

    // ============ 11. VISUAL FEEDBACK (Instant feel) ============
    // Add active state immediately on click
    document.addEventListener('click', function(e) {
        const link = e.target.closest('a');
        if (link && link.href && link.href.startsWith(window.location.origin)) {
            // Add instant visual feedback
            link.style.opacity = '0.7';
            link.style.transition = 'opacity 0.1s';
            
            // Add loading indicator to body
            document.body.style.cursor = 'wait';
            
            // Will be restored when new page loads
        }
    });

    // ============ 12. DNS PREFETCH FOR EXTERNAL RESOURCES ============
    const externalDomains = [
        'https://fonts.googleapis.com',
        'https://fonts.gstatic.com',
        'https://cdn.jsdelivr.net'
    ];

    externalDomains.forEach(domain => {
        const link = document.createElement('link');
        link.rel = 'dns-prefetch';
        link.href = domain;
        document.head.appendChild(link);
    });

    // ============ 13. RESOURCE HINTS FOR NEXT PAGE ============
    function addResourceHints(url) {
        // Add preconnect for the page
        const preconnect = document.createElement('link');
        preconnect.rel = 'preconnect';
        preconnect.href = window.location.origin;
        document.head.appendChild(preconnect);
    }

    // ============ 14. NAVIGATION TIMING (Debug) ============
    window.addEventListener('load', function() {
        if (window.performance && window.performance.timing) {
            const timing = window.performance.timing;
            const navigationStart = timing.navigationStart;
            const loadComplete = timing.loadEventEnd;
            const loadTime = loadComplete - navigationStart;
            
            console.log(`⏱️ Page load time: ${loadTime}ms`);
            
            // Log to console if slow
            if (loadTime > 1000) {
                console.warn('⚠️ Slow page load detected. Consider optimizing.');
            } else {
                console.log('⚡ Page loaded fast! Great job!');
            }
        }
    });

    // ============ 15. SMART PREFETCH PRIORITIZATION ============
    // Prioritize navbar links over other links
    function smartPrefetch() {
        // High priority: Navbar links
        const navLinks = Array.from(document.querySelectorAll('.nav-link, nav a'));
        
        // Medium priority: Buttons and CTAs
        const ctaLinks = Array.from(document.querySelectorAll('.btn[href], .cta a'));
        
        // Low priority: Footer links
        const footerLinks = Array.from(document.querySelectorAll('footer a'));
        
        // Prefetch in order of priority
        [...navLinks, ...ctaLinks, ...footerLinks].forEach((link, index) => {
            const href = link.getAttribute('href');
            if (href && href.startsWith('/')) {
                setTimeout(() => {
                    prefetchUrl(href);
                }, index * 50);
            }
        });
    }

    // Run smart prefetch after 2 seconds
    setTimeout(smartPrefetch, 2000);

    // ============ INITIALIZATION MESSAGE ============
    console.log('⚡⚡⚡ INSTANT NAVIGATION: ACTIVE ⚡⚡⚡');
    console.log('Navbar links will load instantly!');
    console.log('Prefetch strategy: Aggressive + Prerender');

    // Export for debugging
    window.instantNav = {
        prefetchedUrls: prefetchedUrls,
        prefetchUrl: prefetchUrl,
        config: config
    };
})();

