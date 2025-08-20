Whole project consists of many modules:

1. SetupController:
    - responsible for setting initial values of simulation parameters, that will not change later (ex. NumberOfParticles)
    - generate initial grid of particle positions
    - generate random initial velocities
    - pass information and start other modules

2. InputHandler:
    - checks for key and mouse button clicks
        a) SPACE        - pause simulation
        b) ESC          - main menu (change gravity, density, reset simulation etc) - controlled by UIController
        c) mouse button - display bucket (upon mouse click it attracts (left) or repels (right) particles) 
    - setup nearest neighbour grid dispaly

3. Displayer:
    - display particles and dummyParticles (particles at the boundary)
    - uses Particle2D.shader - colors particles based on velocity
    - draws in one call all meshes directly on GPU
    - uses ComputeHelper - outside resource for help with shader operations - https://youtu.be/rSKMYc1CQHE?si=A8yQyoEhd40MnnWV

4. Sim:      <-- MAIN MODULE
    It consists of 4 main classes:

    a) Sim - general communicator, invokes all the commands, stores all the simulation information
        -> Uses parallel computation to speed calculations of forces
        -> in the future whole content should be turned into comput shader - to use great paralellization opportunity

    b) Hash - responsible for nearest neighbours search:
        () https://youtu.be/rSKMYc1CQHE?si=jIil9EC2AUQV6CSU
        -> each particle has pair (index, hash), hash is based on positions
        -> array sorted by hash
        -> create array start_indeces[# hashes] - for each hash stores index of first particle in cell in LookupTable
        -> function FindNeighbours: 
            hash  = calculate hash
            start = start_indeces[hash]
            iterate i = from start to when hash is different
                neighbour_index = LookupTable[i]
                AnyFunction(neighbour_index);
            end
    
    c) RKN4 - Runge Kutta Nystrom 4th order algorithm
        () https://en.wikipedia.org/wiki/Runge%E2%80%93Kutta_methods#Nonconfluent_Runge%E2%80%93Kutta_methods

    d) SPH - Smoothed Particle Module
        () Uses main idea of SPH - we smooth a kernel over smoothing radius,
        kernel must statisfy some conditions (dirac delta limit, constant volume, etc)
        we approximate continious integral over kernel support by sum over particles inside smoothin radius
        we can then calculate any property A for particle i from particles in its neighbourhood
        A_i = sum_{j = neighbours(i)}: mass_j / density_j * A_j * KernelFunction(a, b);
        () https://sci-hub.se/https://doi.org/10.1016/j.jcp.2012.05.005
        () https://spiral.imperial.ac.uk/entities/publication/638bc8d7-21f4-4202-a771-dce6b87f2e7d
        -> dummyParticles used to enforce no-slip boundary conditions
        -> calculates densities
        -> calculates dummyParticles velocities (for boundary condition)

        for relation p(rho) almost compressible approximation is used
        p(rho) = rho_0 * c^2 / gamma * ( (rho/rho_0)^gamma - 1 );
        where 
            - rho_0 is refrence density
            - rho is density
            - c is speed of sound (set to be around 10x max speed of fluid particles)
            - gamma=3 - c_p / c_r - for fluids typically taken to be 7 - in our simulation it is 3 for stability  reason
                   ^-- to be improved in the future

        -> calculate acceleration acting on each particle
            - pressure (grad p / rho)
            - viscous (nu * laplacian u)
            - artificial viscosity - for simulation stability (effectively slightly higher viscosity)
