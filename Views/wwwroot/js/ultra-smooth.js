/**
 * IMPERIAL VIP - ULTRA SMOOTH
 * GARANTILI smooth geçişler - Flash %100 YOK!
 */

(function() {
    'use strict';
    
    console.log('🚀 ULTRA SMOOTH: Starting...');
    
    // ============ 1. IMMEDIATE: Prevent white flash ============
    document.documentElement.style.backgroundColor = '#f8f9fa';
    document.body.style.backgroundColor = '#f8f9fa';
    
    // ============ 2. INTERCEPT ALL NAVIGATION ============
    function interceptNavigation() {
        // Get all navbar links
        const links = document.querySelectorAll('nav a, .nav-link, .navbar a, a[href^="/"]');
        
        links.forEach(link => {
            link.addEventListener('click', function(e) {
                const href = this.getAttribute('href');
                
                // Skip external, anchors, or current page
                if (!href || 
                    href.startsWith('http') || 
                    href.startsWith('#') || 
                    href === window.location.pathname) {
                    return;
                }
                
                // Prevent default
                e.preventDefault();
                
                // Smooth navigate
                smoothNavigate(href);
            });
        });
        
        console.log(`✅ Intercepted ${links.length} links`);
    }
    
    // ============ 3. SMOOTH NAVIGATE FUNCTION ============
    function smoothNavigate(url) {
        console.log(`🎬 Navigating to: ${url}`);
        
        // Add loading class
        document.body.classList.add('page-loading');
        
        // Wait for fade
        setTimeout(() => {
            // Navigate
            window.location.href = url;
        }, 200); // Match CSS transition duration
    }
    
    // ============ 4. FADE IN ON PAGE LOAD ============
    function fadeInPage() {
        // Remove loading class
        document.body.classList.remove('page-loading');
        
        // Ensure visible
        document.body.style.opacity = '1';
        
        console.log('✅ Page faded in');
    }
    
    // ============ 5. HANDLE BACK/FORWARD ============
    window.addEventListener('pageshow', function(e) {
        if (e.persisted) {
            // Page from cache
            console.log('⚡ From cache');
            fadeInPage();
        }
    });
    
    // ============ 6. PRELOAD CRITICAL PAGES ============
    function preloadCritical() {
        const criticalPages = [
            '/Vehicle/Index',
            '/Home/AllRegions', 
            '/Reservation/Index',
            '/Home/About',
            '/Home/Galeri'
        ];
        
        criticalPages.forEach((page, i) => {
            setTimeout(() => {
                const link = document.createElement('link');
                link.rel = 'prefetch';
                link.href = page;
                document.head.appendChild(link);
                console.log(`⚡ Prefetched: ${page}`);
            }, i * 100);
        });
    }
    
    // ============ INITIALIZATION ============
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            fadeInPage();
            interceptNavigation();
            setTimeout(preloadCritical, 500);
        });
    } else {
        fadeInPage();
        interceptNavigation();
        setTimeout(preloadCritical, 500);
    }
    
    console.log('✅ ULTRA SMOOTH: ACTIVE!');
    
})();

