// Category Manager - Handles component categorization and filtering

const componentCategories = {
    'all': {
        name: 'All',
        icon: '📊',
        filter: (comp) => true
    },
    'sensors': {
        name: 'Sensors',
        icon: '🔍',
        filter: (comp) => comp.type === 'Sensor'
    },
    'conveyors': {
        name: 'Conveyors',
        icon: '⚙️',
        filter: (comp) => {
            if (comp.type !== 'Drive') return false;
            const name = comp.name.toLowerCase();
            return name.includes('conveyor') || 
                   name.includes('belt') || 
                   name.includes('transport') ||
                   comp.hasTransportSurface === true;
        }
    },
    'robots': {
        name: 'Robots',
        icon: '🤖',
        filter: (comp) => {
            return (comp.type === 'Axis' || comp.type === 'Grip') && 
                   comp.parentRobot !== null;
        }
    },
    'lights': {
        name: 'Lights',
        icon: '💡',
        filter: (comp) => comp.type === 'Lamp'
    },
    'sources': {
        name: 'Sources',
        icon: '📦',
        filter: (comp) => comp.type === 'Source'
    },
    'axes': {
        name: 'Axes',
        icon: '↔️',
        filter: (comp) => comp.type === 'Axis' && comp.parentRobot === null
    },
    'grippers': {
        name: 'Grippers',
        icon: '🤏',
        filter: (comp) => comp.type === 'Grip' && comp.parentRobot === null
    },
    'sinks': {
        name: 'Sinks',
        icon: '📤',
        filter: (comp) => comp.type === 'Sink'
    }
};

let currentCategory = 'all';
let allComponents = [];
let categoryCounts = {};

// Initialize category manager
function initCategoryManager() {
    renderCategoryTabs();
    loadCategoryCounts();
}

// Render category tabs - only shows categories with components (except 'all')
function renderCategoryTabs() {
    const tabsContainer = document.getElementById('categoryTabs');
    if (!tabsContainer) return;
    
    // Calculate counts for each category
    Object.keys(componentCategories).forEach(categoryId => {
        componentCategories[categoryId].count = 
            allComponents.filter(componentCategories[categoryId].filter).length;
    });
    
    // Filter out empty categories (except 'all' which always shows)
    const visibleCategories = Object.entries(componentCategories)
        .filter(([id, category]) => id === 'all' || category.count > 0);
    
    // If current category became empty, switch to 'all'
    const currentCategoryData = componentCategories[currentCategory];
    if (currentCategory !== 'all' && currentCategoryData && currentCategoryData.count === 0) {
        currentCategory = 'all';
    }
    
    // Render tabs - only non-empty categories
    tabsContainer.innerHTML = visibleCategories
        .map(([id, category]) => `
            <button class="category-tab ${currentCategory === id ? 'active' : ''}" 
                    data-category="${id}"
                    onclick="switchCategory('${id}')">
                <span class="category-icon">${category.icon}</span>
                <span class="category-name">${category.name}</span>
                <span class="category-count">(${category.count || 0})</span>
            </button>
        `).join('');
}

// Switch category
function switchCategory(categoryId) {
    if (!componentCategories[categoryId]) {
        console.error(`Unknown category: ${categoryId}`);
        return;
    }
    
    currentCategory = categoryId;
    renderCategoryTabs();
    
    // Filter and render components
    if (typeof updateComponents === 'function') {
        updateComponents();
    }
    
    // Clear selection when switching categories
    selectedComponent = null;
    if (typeof renderControlPanel === 'function') {
        renderControlPanel(null);
    }
    if (typeof renderStatusPanel === 'function') {
        renderStatusPanel(null);
    }
}

// Get filtered components for current category
function getFilteredComponents() {
    if (!allComponents || allComponents.length === 0) return [];
    
    const category = componentCategories[currentCategory];
    if (!category) return allComponents;
    
    return allComponents.filter(category.filter);
}

// Load category counts from API
async function loadCategoryCounts() {
    try {
        const response = await fetch(API_CONFIG.baseUrl + '/api/categories');
        if (response.ok) {
            const data = await response.json();
            if (data.success && data.categories) {
                categoryCounts = data.categories;
                // Update category counts
                Object.keys(componentCategories).forEach(categoryId => {
                    if (categoryCounts[categoryId] !== undefined) {
                        componentCategories[categoryId].count = categoryCounts[categoryId];
                    }
                });
                renderCategoryTabs();
            }
        }
    } catch (error) {
        console.error('Error loading category counts:', error);
    }
}

// Get current category
function getCurrentCategory() {
    return currentCategory;
}

// Get category filter
function getCategoryFilter(categoryId) {
    if (componentCategories[categoryId]) {
        return componentCategories[categoryId].filter;
    }
    return null;
}



