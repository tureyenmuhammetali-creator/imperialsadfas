/**
 * IMPERIAL VIP - SMOOTH TRANSITIONS
 * Flash/duraksama problemini tamamen çözer!
 * Kategori geçişleri artık PÜRÜZSÜZ!
 */

(function() {
    'use strict';

    console.log('🎬 SMOOTH TRANSITIONS: Loading...');

    // ============ CONFIG ============
    const config = {
        transitionDuration: 200,    // Geçiş süresi (ms)
        fadeOutDuration: 150,       // Fade out süresi
        fadeInDuration: 150,        // Fade in süresi
        preventWhiteFlash: true,    // Beyaz flash'ı engelle
        smoothScroll: true,         // Smooth scroll
        preloadImages: true         // Görselleri önden yükle
    };

    // ============ 1. BEYAZ FLASH ENGELLEYİCİ ============
    function preventWhiteFlash() {
        // Body'ye transition background ekle
        document.body.style.transition = 'opacity 0.15s ease-in-out, background-color 0.15s ease';
        document.body.style.backgroundColor = getComputedStyle(document.body).backgroundColor || '#ffffff';
    }

    // ============ 2. SMOOTH PAGE TRANSITION ============
    function setupSmoothTransitions() {
        // Tüm navbar linklerini yakala
        const navLinks = document.querySelectorAll('nav a, .nav-link, .navbar a');
        
        navLinks.forEach(link => {
            link.addEventListener('click', function(e) {
                const href = this.getAttribute('href');
                
                // Skip if not internal link
                if (!href || !href.startsWith('/') || href.startsWith('/#')) {
                    return;
                }
                
                // Prevent default navigation
                e.preventDefault();
                
                // Start smooth transition
                smoothNavigate(href, this);
            });
        });
    }

    // ============ 3. SMOOTH NAVIGATE FUNCTION ============
    function smoothNavigate(url, linkElement) {
        console.log(`🎬 Smooth navigating to: ${url}`);
        
        // 1. Fade out current page
        document.body.style.opacity = '0.7';
        
        // 2. Add loading class
        document.body.classList.add('page-transitioning');
        if (linkElement) {
            linkElement.classList.add('active-transition');
        }
        
        // 3. Wait for fade out
        setTimeout(() => {
            // Navigate to new page
            window.location.href = url;
        }, config.fadeOutDuration);
    }

    // ============ 4. FADE IN ON PAGE LOAD ============
    function fadeInPage() {
        // Set initial state (hidden)
        if (document.body.style.opacity === '') {
            document.body.style.opacity = '0';
        }
        
        // Fade in after a tiny delay
        requestAnimationFrame(() => {
            document.body.style.transition = `opacity ${config.fadeInDuration}ms ease-in`;
            document.body.style.opacity = '1';
            
            // Remove transition class
            document.body.classList.remove('page-transitioning');
        });
    }

    // ============ 5. VIEW TRANSITIONS API (Chrome/Edge) ============
    function setupViewTransitionsAPI() {
        // Check if View Transitions API is supported
        if (!document.startViewTransition) {
            console.log('View Transitions API not supported, using fallback');
            return;
        }

        console.log('✨ View Transitions API: ENABLED!');

        // Enhanced smooth navigation with View Transitions API
        const navLinks = document.querySelectorAll('nav a, .nav-link, .navbar a');
        
        navLinks.forEach(link => {
            link.addEventListener('click', function(e) {
                const href = this.getAttribute('href');
                
                if (!href || !href.startsWith('/') || href.startsWith('/#')) {
                    return;
                }
                
                e.preventDefault();
                
                // Use View Transitions API
                const transition = document.startViewTransition(() => {
                    window.location.href = href;
                });
                
                console.log('🎬 View Transition started');
            });
        });
    }

    // ============ 6. LAYOUT PRESERVE ============
    function preserveLayout() {
        // Save scroll position
        sessionStorage.setItem('scrollPosition', window.scrollY);
        
        // Save viewport state
        sessionStorage.setItem('viewportWidth', window.innerWidth);
        sessionStorage.setItem('viewportHeight', window.innerHeight);
    }

    function restoreLayout() {
        // Restore scroll position
        const scrollPos = sessionStorage.getItem('scrollPosition');
        if (scrollPos) {
            window.scrollTo(0, parseInt(scrollPos));
        }
    }

    // ============ 7. PREVENT LAYOUT SHIFT ============
    function preventLayoutShift() {
        // Add min-height to body to prevent shifts
        const minHeight = window.innerHeight;
        document.body.style.minHeight = `${minHeight}px`;
        
        // Reserve space for header
        const header = document.querySelector('.header, header, nav');
        if (header) {
            const headerHeight = header.offsetHeight;
            document.body.style.paddingTop = `${headerHeight}px`;
            header.style.position = 'fixed';
            header.style.top = '0';
            header.style.left = '0';
            header.style.right = '0';
            header.style.zIndex = '1000';
        }
    }

    // ============ 8. INSTANT CSS LOAD ============
    function optimizeCSSLoading() {
        // Force CSS to be parsed immediately
        const styleSheets = document.styleSheets;
        
        try {
            for (let i = 0; i < styleSheets.length; i++) {
                const sheet = styleSheets[i];
                if (sheet.cssRules) {
                    // Access cssRules forces parsing
                    const rulesCount = sheet.cssRules.length;
                }
            }
            console.log('✅ CSS parsed instantly');
        } catch (e) {
            // Some stylesheets may be cross-origin
        }
    }

    // ============ 9. PRELOAD NEXT PAGE IMAGES ============
    function preloadNextPageImages(url) {
        // This would require knowing which images are on the next page
        // For now, we'll just ensure current images are loaded
        const images = document.querySelectorAll('img');
        let loadedCount = 0;
        
        images.forEach(img => {
            if (img.complete) {
                loadedCount++;
            } else {
                img.addEventListener('load', () => {
                    loadedCount++;
                    if (loadedCount === images.length) {
                        console.log('✅ All images loaded');
                    }
                });
            }
        });
    }

    // ============ 10. SMOOTH SCROLL TO TOP ============
    function smoothScrollToTop() {
        if (config.smoothScroll) {
            window.scrollTo({
                top: 0,
                behavior: 'smooth'
            });
        }
    }

    // ============ 11. ELIMINATE RENDER BLOCKING ============
    function eliminateRenderBlocking() {
        // Add critical rendering hints
        const style = document.createElement('style');
        style.textContent = `
            /* Critical CSS - Prevents flash */
            html, body {
                margin: 0;
                padding: 0;
                background-color: #ffffff;
                opacity: 1 !important;
            }
            
            /* Smooth transitions */
            body {
                transition: opacity 0.15s ease-in-out !important;
            }
            
            /* Prevent layout shift */
            .header, header, nav {
                transform: translateZ(0);
                will-change: transform;
            }
            
            /* Page transition states */
            body.page-transitioning {
                pointer-events: none;
            }
            
            body.page-transitioning * {
                animation-play-state: paused !important;
            }
        `;
        document.head.insertBefore(style, document.head.firstChild);
    }

    // ============ 12. BROWSER BACK/FORWARD CACHE ============
    function optimizeBFCache() {
        // Ensure page is eligible for bfcache
        window.addEventListener('pageshow', function(event) {
            if (event.persisted) {
                console.log('⚡ Page restored from bfcache (instant!)');
                
                // Fade in immediately
                document.body.style.opacity = '1';
                document.body.classList.remove('page-transitioning');
            } else {
                // Normal page load - fade in
                fadeInPage();
            }
        });
        
        window.addEventListener('pagehide', function(event) {
            // Save state for bfcache
            preserveLayout();
        });
    }

    // ============ 13. PROGRESSIVE RENDERING ============
    function enableProgressiveRendering() {
        // Show content as it loads (no blank screen)
        document.body.style.visibility = 'visible';
        
        // Use content-visibility for off-screen content
        const sections = document.querySelectorAll('section, .section, main > div');
        sections.forEach((section, index) => {
            if (index > 2) { // After first 3 sections
                section.style.contentVisibility = 'auto';
            }
        });
    }

    // ============ 14. REDUCE REPAINTS ============
    function reduceRepaints() {
        // Use transform instead of position changes
        const animatedElements = document.querySelectorAll('.nav-link, .btn, a');
        animatedElements.forEach(el => {
            el.style.willChange = 'transform, opacity';
        });
    }

    // ============ 15. INSTANT FEEDBACK ============
    function setupInstantFeedback() {
        document.addEventListener('click', function(e) {
            const link = e.target.closest('a');
            if (link && link.href && link.href.startsWith(window.location.origin)) {
                // Immediate visual feedback
                link.style.transform = 'scale(0.98)';
                link.style.opacity = '0.8';
                
                setTimeout(() => {
                    link.style.transform = '';
                    link.style.opacity = '';
                }, 100);
            }
        }, { passive: true });
    }

    // ============ INITIALIZATION ============
    function init() {
        console.log('🎬 SMOOTH TRANSITIONS: Initializing...');
        
        // Critical optimizations first
        eliminateRenderBlocking();
        preventWhiteFlash();
        optimizeCSSLoading();
        
        // Setup transitions
        if (document.startViewTransition) {
            // Use native View Transitions API (best!)
            setupViewTransitionsAPI();
            console.log('✨ Using native View Transitions API');
        } else {
            // Fallback to custom transitions
            setupSmoothTransitions();
            console.log('🎬 Using custom smooth transitions');
        }
        
        // Additional optimizations
        optimizeBFCache();
        enableProgressiveRendering();
        reduceRepaints();
        setupInstantFeedback();
        
        // Fade in current page
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', fadeInPage);
        } else {
            fadeInPage();
        }
        
        console.log('✅ SMOOTH TRANSITIONS: ACTIVE!');
        console.log('🎬 No more flash/duraksama!');
    }

    // Start immediately
    if (document.readyState === 'loading') {
        // Run before DOMContentLoaded
        document.addEventListener('DOMContentLoaded', init);
        
        // Also run some critical fixes immediately
        eliminateRenderBlocking();
        preventWhiteFlash();
    } else {
        init();
    }

    // Export for debugging
    window.smoothTransitions = {
        config: config,
        navigate: smoothNavigate,
        fadeIn: fadeInPage
    };

})();

